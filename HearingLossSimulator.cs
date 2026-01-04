using System;
using System.Diagnostics;
using System.Threading;
using OpenTK.Audio.OpenAL;

namespace HearingLossSimulator
{
    public class HearingLossSimulator
    {
        private string captureDeviceName;
        private ALCaptureDevice captureDevice;
        private ALDevice playbackDevice;
        private ALContext context;
        
        private int source;
        private int[]? buffers;
        private const int BUFFER_COUNT = 4;
        private const int SAMPLE_RATE = 44100;
        private const int BUFFER_SIZE = 2048;
        
        private AudioProcessor? processor;
        private AudiologicalProfile profile;
        private bool useHRTF;
        
        private float inputLevel;
        private float outputLevel;
        private Stopwatch latencyTimer;
        private double latencyMs;
        private bool isRunning;
        private Thread? captureThread;

        public HearingLossSimulator(string captureDev, AudiologicalProfile prof, bool hrtf)
        {
            captureDeviceName = captureDev;
            profile = prof;
            useHRTF = hrtf;
            latencyTimer = new Stopwatch();
        }

        public bool Initialize()
        {
            try
            {
                // Initialisation périphérique de lecture (sortie)
                playbackDevice = ALC.OpenDevice(null);
                if (playbackDevice == ALDevice.Null)
                {
                    Console.WriteLine("❌ Impossible d'ouvrir le périphérique de lecture");
                    return false;
                }

                // Création du contexte
                context = ALC.CreateContext(playbackDevice, (int[]?)null);
                if (context == ALContext.Null)
                {
                    Console.WriteLine("❌ Impossible de créer le contexte OpenAL");
                    return false;
                }

                ALC.MakeContextCurrent(context);

                // Génération des sources et buffers
                source = AL.GenSource();
                buffers = AL.GenBuffers(BUFFER_COUNT);

                // Configuration de la source
                AL.Source(source, ALSourcef.Gain, 1.0f);
                AL.Source(source, ALSourcef.Pitch, 1.0f);
                AL.Source(source, ALSource3f.Position, 0, 0, 0);

                // Information sur HRTF
                if (useHRTF)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  HRTF: Activé (géré par OpenAL Soft)");
                    Console.ResetColor();
                }

                // Initialisation du processeur audio
                processor = new AudioProcessor(profile, useHRTF);

                // Initialisation capture
                captureDevice = ALC.CaptureOpenDevice(
                    captureDeviceName, 
                    SAMPLE_RATE, 
                    ALFormat.Mono16, 
                    BUFFER_SIZE * 2
                );

                if (captureDevice == ALCaptureDevice.Null)
                {
                    Console.WriteLine("❌ Impossible d'ouvrir le périphérique de capture");
                    return false;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Simulation initialisée avec succès!\n");
                Console.ResetColor();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur d'initialisation: {ex.Message}");
                return false;
            }
        }

        public void Start()
        {
            isRunning = true;
            latencyTimer.Start();

            // Démarrage de la capture
            ALC.CaptureStart(captureDevice);

            // Démarrage du thread de traitement
            captureThread = new Thread(CaptureLoop);
            captureThread.Start();
        }

        private void CaptureLoop()
        {
            short[] captureBuffer = new short[BUFFER_SIZE];
            int buffersQueued = 0;
            
            while (isRunning)
            {
                // Vérification des échantillons disponibles
                int samplesAvailable = ALC.GetAvailableSamples(captureDevice);

                if (samplesAvailable >= BUFFER_SIZE)
                {
                    var startTime = latencyTimer.Elapsed.TotalMilliseconds;

                    // Capture des échantillons
                    unsafe
                    {
                        fixed (short* ptr = captureBuffer)
                        {
                            ALC.CaptureSamples(captureDevice, (IntPtr)ptr, BUFFER_SIZE);
                        }
                    }

                    // Calcul du niveau d'entrée
                    inputLevel = CalculateRMS(captureBuffer);

                    // Traitement audio
                    short[] processedBuffer = processor!.Process(captureBuffer);

                    // Calcul du niveau de sortie
                    outputLevel = CalculateRMS(processedBuffer);

                    // Conversion en stéréo pour la lecture
                    short[] stereoBuffer = ConvertToStereo(processedBuffer);

                    // Envoi vers OpenAL
                    if (buffersQueued < BUFFER_COUNT)
                    {
                        // Phase d'initialisation : remplir tous les buffers
                        QueueInitialBuffer(stereoBuffer, buffersQueued);
                        buffersQueued++;
                        
                        // Démarrer la lecture une fois qu'on a au moins 2 buffers
                        if (buffersQueued == 2)
                        {
                            AL.SourcePlay(source);
                        }
                    }
                    else
                    {
                        // Phase normale : remplacer les buffers traités
                        QueueAudioBuffer(stereoBuffer);
                    }

                    // Calcul de la latence
                    var endTime = latencyTimer.Elapsed.TotalMilliseconds;
                    latencyMs = endTime - startTime;
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }

        private void QueueInitialBuffer(short[] data, int bufferIndex)
        {
            if (buffers == null || bufferIndex >= BUFFER_COUNT) return;
            
            unsafe
            {
                fixed (short* ptr = data)
                {
                    AL.BufferData(buffers[bufferIndex], ALFormat.Stereo16, (IntPtr)ptr, data.Length * sizeof(short), SAMPLE_RATE);
                }
            }
            
            AL.SourceQueueBuffer(source, buffers[bufferIndex]);
        }

        private void QueueAudioBuffer(short[] data)
        {
            // Récupération d'un buffer traité
            AL.GetSource(source, ALGetSourcei.BuffersProcessed, out int buffersProcessed);
            
            if (buffersProcessed > 0)
            {
                // Déqueue et remplir avec nouvelles données
                int buffer = AL.SourceUnqueueBuffer(source);
                
                unsafe
                {
                    fixed (short* ptr = data)
                    {
                        AL.BufferData(buffer, ALFormat.Stereo16, (IntPtr)ptr, data.Length * sizeof(short), SAMPLE_RATE);
                    }
                }
                
                AL.SourceQueueBuffer(source, buffer);
            }

            // Redémarrage si la source s'est arrêtée
            AL.GetSource(source, ALGetSourcei.SourceState, out int state);
            if ((ALSourceState)state != ALSourceState.Playing)
            {
                AL.SourcePlay(source);
            }
        }

        private short[] ConvertToStereo(short[] mono)
        {
            short[] stereo = new short[mono.Length * 2];
            for (int i = 0; i < mono.Length; i++)
            {
                stereo[i * 2] = mono[i];
                stereo[i * 2 + 1] = mono[i];
            }
            return stereo;
        }

        private float CalculateRMS(short[] buffer)
        {
            long sum = 0;
            foreach (short sample in buffer)
            {
                sum += sample * sample;
            }
            
            double mean = sum / (double)buffer.Length;
            double rms = Math.Sqrt(mean);
            
            return (float)(rms / 32768.0);
        }

        public void UpdateDisplay()
        {
            Console.SetCursorPosition(0, 6);
            
            // Affichage configuration
            string profileName = profile == AudiologicalProfile.Normal ? "Audition Normale" :
                                profile == AudiologicalProfile.MildLoss ? "Surdité Légère" : "Surdité Moyenne";
            Console.WriteLine($"Profil: {profileName}");
            Console.WriteLine($"HRTF: {(useHRTF ? "Activé (OpenAL Soft)" : "Désactivé")}");
            Console.WriteLine();

            // Niveau d'entrée
            Console.Write("📥 Entrée:  ");
            DrawVUMeter(inputLevel, ConsoleColor.Green);
            Console.WriteLine($" {(inputLevel * 100):F1}%  ");

            // Niveau de sortie
            Console.Write("📤 Sortie:  ");
            DrawVUMeter(outputLevel, ConsoleColor.Cyan);
            Console.WriteLine($" {(outputLevel * 100):F1}%  ");

            // Latence
            Console.WriteLine();
            Console.ForegroundColor = latencyMs < 20 ? ConsoleColor.Green : 
                                      latencyMs < 50 ? ConsoleColor.Yellow : ConsoleColor.Red;
            Console.WriteLine($"⏱️  Latence: {latencyMs:F2} ms         ");
            Console.ResetColor();

            // État de la source
            AL.GetSource(source, ALGetSourcei.BuffersQueued, out int queued);
            AL.GetSource(source, ALGetSourcei.BuffersProcessed, out int processed);
            int samplesAvailable = ALC.GetAvailableSamples(captureDevice);
            
            Console.WriteLine($"📊 Buffers: {queued} en queue, {processed} traités    ");
            Console.WriteLine($"🎤 Samples disponibles: {samplesAvailable}        ");
            
            // État de la source
            AL.GetSource(source, ALGetSourcei.SourceState, out int state);
            string stateStr = ((ALSourceState)state).ToString();
            Console.WriteLine($"▶️  État source: {stateStr}        ");
        }

        private void DrawVUMeter(float level, ConsoleColor color)
        {
            const int meterWidth = 40;
            int filled = (int)(level * meterWidth);
            
            Console.ForegroundColor = color;
            Console.Write("[");
            for (int i = 0; i < meterWidth; i++)
            {
                Console.Write(i < filled ? "█" : "░");
            }
            Console.Write("]");
            Console.ResetColor();
        }

        public void Stop()
        {
            isRunning = false;
            
            if (captureThread != null && captureThread.IsAlive)
            {
                captureThread.Join(1000);
            }

            if (captureDevice != ALCaptureDevice.Null)
            {
                ALC.CaptureStop(captureDevice);
                ALC.CaptureCloseDevice(captureDevice);
            }

            if (source != 0)
            {
                AL.SourceStop(source);
                AL.DeleteSource(source);
            }

            if (buffers != null)
            {
                AL.DeleteBuffers(buffers);
            }

            if (context != ALContext.Null)
            {
                ALC.MakeContextCurrent(ALContext.Null);
                ALC.DestroyContext(context);
            }

            if (playbackDevice != ALDevice.Null)
            {
                ALC.CloseDevice(playbackDevice);
            }
        }
    }
}
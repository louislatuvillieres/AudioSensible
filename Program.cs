using System;
using System.Linq;
using System.Threading;
using OpenTK.Audio.OpenAL;

namespace HearingLossSimulator
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine("    SIMULATEUR DE SURDITÉ - Version Multiplateforme");
            Console.WriteLine("═══════════════════════════════════════════════════\n");

            try
            {
                // Vérification OpenAL
                if (!CheckOpenALAvailability())
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ OpenAL n'est pas disponible sur ce système.");
                    Console.WriteLine("   Installez OpenAL Soft depuis https://openal-soft.org/");
                    Console.ResetColor();
                    Console.ReadKey();
                    return;
                }

                // 1. Sélection périphérique de capture
                var captureDevice = SelectCaptureDevice();
                if (captureDevice == null) return;

                // 2. Sélection profil audiologique
                var profile = SelectAudiologicalProfile();

                // 3. Activation HRTF
                var useHRTF = SelectHRTFOption();

                // 4. Démarrage simulation
                Console.Clear();
                RunSimulation(captureDevice, profile, useHRTF);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n❌ Erreur: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                Console.ResetColor();
                Console.WriteLine("\nAppuyez sur une touche pour quitter...");
                Console.ReadKey();
            }
        }

        static bool CheckOpenALAvailability()
        {
            try
            {
                var deviceList = ALC.GetStringList(GetEnumerationStringList.DeviceSpecifier);
                return deviceList != null && deviceList.Any();
            }
            catch
            {
                return false;
            }
        }

        static string? SelectCaptureDevice()
        {
            Console.WriteLine("📥 SÉLECTION DU PÉRIPHÉRIQUE DE CAPTURE");
            Console.WriteLine("─────────────────────────────────────────────────\n");

            var deviceList = ALC.GetStringList(GetEnumerationStringList.CaptureDeviceSpecifier);
            var devices = deviceList?.ToList();
            
            if (devices == null || devices.Count == 0)
            {
                Console.WriteLine("❌ Aucun périphérique de capture détecté.");
                Console.ReadKey();
                return null;
            }

            for (int i = 0; i < devices.Count; i++)
            {
                Console.WriteLine($"  [{i}] {devices[i]}");
            }

            Console.Write($"\n➤ Sélectionnez le périphérique (0-{devices.Count - 1}): ");
            
            if (int.TryParse(Console.ReadLine(), out int selection) && 
                selection >= 0 && selection < devices.Count)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Sélectionné: {devices[selection]}\n");
                Console.ResetColor();
                return devices[selection];
            }

            Console.WriteLine("❌ Sélection invalide.");
            Console.ReadKey();
            return null;
        }

        static AudiologicalProfile SelectAudiologicalProfile()
        {
            Console.WriteLine("🔊 SÉLECTION DU PROFIL AUDIOLOGIQUE");
            Console.WriteLine("─────────────────────────────────────────────────\n");
            Console.WriteLine("  [0] Audition Normale (aucun filtre)");
            Console.WriteLine("      • Écoute sans simulation de perte\n");
            
            Console.WriteLine("  [1] Surdité Légère (20-40 dB HL)");
            Console.WriteLine("      • Difficulté avec sons faibles");
            Console.WriteLine("      • Conversations calmes difficiles\n");
            
            Console.WriteLine("  [2] Surdité Moyenne (40-70 dB HL)");
            Console.WriteLine("      • Nécessite élévation de voix");
            Console.WriteLine("      • Difficulté conversations normales\n");

            Console.Write("➤ Sélectionnez le profil (0-2): ");
            var choice = Console.ReadLine();

            AudiologicalProfile profile;
            
            if (choice == "0")
            {
                profile = AudiologicalProfile.Normal;
            }
            else if (choice == "2")
            {
                profile = AudiologicalProfile.ModerateLoss;
            }
            else
            {
                profile = AudiologicalProfile.MildLoss;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            string profileName = profile == AudiologicalProfile.Normal ? "Audition Normale" :
                                profile == AudiologicalProfile.MildLoss ? "Surdité Légère" : "Surdité Moyenne";
            Console.WriteLine($"✓ Profil: {profileName}\n");
            Console.ResetColor();

            return profile;
        }

        static bool SelectHRTFOption()
        {
            Console.WriteLine("🎧 SIMULATION HRTF (Head-Related Transfer Function)");
            Console.WriteLine("─────────────────────────────────────────────────\n");
            Console.WriteLine("  [1] Activer HRTF (spatialisation 3D native OpenAL)");
            Console.WriteLine("  [2] Désactiver HRTF (traitement direct)\n");

            Console.Write("➤ Sélectionnez l'option (1-2): ");
            var choice = Console.ReadLine();

            bool useHRTF = choice == "1";

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ HRTF: {(useHRTF ? "Activé (OpenAL Soft)" : "Désactivé")}\n");
            Console.ResetColor();

            return useHRTF;
        }

        static void RunSimulation(string captureDevice, AudiologicalProfile profile, bool useHRTF)
        {
            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine("    SIMULATION EN COURS");
            Console.WriteLine("═══════════════════════════════════════════════════\n");

            var simulator = new HearingLossSimulator(captureDevice, profile, useHRTF);
            
            if (!simulator.Initialize())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Échec de l'initialisation du simulateur.");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }

            simulator.Start();

            Console.WriteLine("Appuyez sur ECHAP pour arrêter la simulation...\n");

            while (true)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                {
                    break;
                }

                simulator.UpdateDisplay();
                Thread.Sleep(100);
            }

            simulator.Stop();
            Console.WriteLine("\n\n✓ Simulation arrêtée.");
            Console.WriteLine("Appuyez sur une touche pour quitter...");
            Console.ReadKey();
        }
    }

    public enum AudiologicalProfile
    {
        Normal,        // Aucun filtre
        MildLoss,      // 20-40 dB HL
        ModerateLoss   // 40-70 dB HL
    }
}
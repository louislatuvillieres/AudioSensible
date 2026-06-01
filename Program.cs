using System;
using System.Threading;

namespace HearingLossSimulator
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine("    SIMULATEUR DE SURDITÉ (ALSA NATIVE)");
            Console.WriteLine("═══════════════════════════════════════════════════\n");

            try
            {
                bool quit = false;

                while (!quit)
                {
                    // 1. Sélection des périphériques audio
                    string captureDevice = SelectDevice(capture: true);
                    string playbackDevice = SelectDevice(capture: false);

                    // 2. Sélection profil audiologique
                    var profile = SelectAudiologicalProfile();

                    // 3. Démarrage simulation
                    Console.Clear();
                    RunSimulation(captureDevice, playbackDevice, profile, out bool reconfigure);
                    quit = !reconfigure;
                }
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

        static string SelectDevice(bool capture)
        {
            string direction = capture ? "ENTRÉE (Microphone)" : "SORTIE (Haut-parleurs)";
            Console.WriteLine($"🎤 SÉLECTION DU PÉRIPHÉRIQUE {direction}");
            Console.WriteLine("─────────────────────────────────────────────────\n");

            var devices = AudioDeviceFactory.EnumerateDevices(capture);

            if (devices.Count == 0)
            {
                Console.WriteLine("Aucun périphérique trouvé. Utilisation de \"default\".\n");
                return "default";
            }

            for (int i = 0; i < devices.Count; i++)
            {
                string firstLine = devices[i].Description.Split('\n')[0];
                Console.WriteLine($"  [{i}] {devices[i].Name}");
                if (!string.IsNullOrEmpty(firstLine))
                    Console.WriteLine($"      {firstLine}");
                Console.WriteLine();
            }

            Console.Write($"➤ Sélectionnez le périphérique (0-{devices.Count - 1}): ");
            string input = Console.ReadLine() ?? "";

            if (int.TryParse(input, out int choice) && choice >= 0 && choice < devices.Count)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Périphérique: {devices[choice].Name}\n");
                Console.ResetColor();
                return devices[choice].Name;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠ Choix invalide. Utilisation de: {devices[0].Name}\n");
            Console.ResetColor();
            return devices[0].Name;
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

        static void RunSimulation(string captureDevice, string playbackDevice,
                                  AudiologicalProfile profile, out bool reconfigure)
        {
            var simulator = new HearingLossSimulator(captureDevice, playbackDevice, profile);
            
            if (!simulator.Initialize())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Échec de l'initialisation du simulateur.");
                Console.ResetColor();
                Console.ReadKey();
                reconfigure = false;
                return;
            }

            simulator.Start();

            // Effacer les messages d'init et poser un header fixe de 6 lignes
            // pour que SetCursorPosition(0, 6) dans UpdateDisplay() tombe au bon endroit
            Console.Clear();
            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine("    SIMULATION EN COURS");
            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine("  [ECHAP] Arrêter   [R] Reconfigurer   [Q] Quitter");
            Console.WriteLine();

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
            Console.WriteLine("\nSimulation arrêtée. [R] Reconfigurer  [Q] Quitter");

            while (true)
            {
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.R)
                {
                    reconfigure = true;
                    return;
                }
                if (key == ConsoleKey.Q || key == ConsoleKey.Escape)
                {
                    reconfigure = false;
                    return;
                }
            }
        }
    }

    public enum AudiologicalProfile
    {
        Normal,        // Aucun filtre
        MildLoss,      // 20-40 dB HL
        ModerateLoss   // 40-70 dB HL
    }
}
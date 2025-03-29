using System.Text.Json;

namespace MapPackerAndUploader
{
    class Program
    {
        const string SettingsFile = "settings.json";

        public Program() { }

        static int Main()
        {
            if (!File.Exists(SettingsFile))
            {
                Logger.LogLine($"The settings file '{SettingsFile}' not found", ConsoleColor.Red);
                return Exit(false);
            }

            ConfigSettings settings;
            JsonElement settingsSection;

            try
            {
                var jsonText = File.ReadAllText(SettingsFile);
                var doc = JsonDocument.Parse(jsonText);

                if (!doc.RootElement.TryGetProperty("settings", out settingsSection))
                {
                    Logger.LogLine($"Missing the 'settings' section in {SettingsFile}", ConsoleColor.Red);
                    return Exit(false);
                }

                var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var nullCheck = JsonSerializer.Deserialize<ConfigSettings>(settingsSection, serializerOptions);

                if (nullCheck == null)
                {
                    Logger.LogLine($"Config file '{SettingsFile}' contains invalid json", ConsoleColor.Red);
                    return Exit(false);
                }

                settings = nullCheck;
            }
            catch (JsonException)
            {
                Logger.LogLine($"Failed to parse '{SettingsFile}' due to invalid json formatting or value types", ConsoleColor.Red);
                return Exit(false);
            }
            catch (Exception e)
            {
                Logger.LogLine(e.Message);
                return Exit(false);
            }
            
            if (!Directory.Exists(settings.GameinfoPath))
            {
                Logger.LogLine($"The directory for the setting 'gameinfoPath' does not exist", ConsoleColor.Red);
                return Exit(false);
            }

            settings.GameinfoPath = settings.GameinfoPath.Replace('\\', '/');

            if (!File.Exists(settings.GameinfoPath + "gameinfo.txt"))
            {
                Logger.LogLine($"The file 'gameinfo.txt' does not exist at the directory of 'gameinfoPath'", ConsoleColor.Red);
                return Exit(false);
            }

            if (!File.Exists(settings.BspzipPath))
            {
                Logger.LogLine($"The file 'bspzip.exe' does not exist at the directory of 'bspzipPath'", ConsoleColor.Red);
                return Exit(false);
            }

            settings.BspzipPath = settings.BspzipPath.Replace('\\', '/');

            if (!Directory.Exists(settings.OutputPath))
            {
                Logger.LogLine($"The directory of 'outputPath' does not exist", ConsoleColor.Red);
                return Exit(false);
            }

            settings.OutputPath = settings.OutputPath.Replace('\\', '/');

            if (!settings.OutputPath.EndsWith('/'))
            {
                settings.OutputPath += '/';
            }

            HashSet<string> exts = [];

            foreach (string ext in settings.ExtensionWhitelist)
            {
                if (string.IsNullOrEmpty(ext))
                {
                    Logger.LogLine("Array values of 'extensionWhitelist' must not contain a null or empty string", ConsoleColor.Red);
                    return Exit(false);
                }

                if (!ext.StartsWith('.'))
                {
                    exts.Add("." + ext);
                }
                else
                {
                    exts.Add(ext);
                }
            }

            settings.ExtensionWhitelist = exts;

            if (settings.BspConfigFileNames.Count == 0)
            {
                Logger.LogLine($"No configs were specified within `bspConfigFileNames`");
                return Exit(false);
            }

            if (settingsSection.TryGetProperty("globalAssets", out JsonElement globalAssetsArray))
            {
                settings.ParseAssets(globalAssetsArray, ref settings.GlobalAssets, "settings");
            }

            settings.ParseConfigs();

            var enabledMaps = 0;
            IList<BspOptions> workshopMaps = [];

            foreach (var bspConfig in settings.BspConfigs)
            {
                foreach (var option in bspConfig.Options)
                {
                    if (!option.Enabled)
                    {
                        continue;
                    }

                    ++enabledMaps;

                    if (settings.UploadMapsToWorkshop && option.Workshop.Upload)
                    {
                        workshopMaps.Add(option);
                    }
                }
            }

            if (enabledMaps == 0)
            {
                Logger.LogLine("Couldn't find any enabled maps from within the list of `bspConfigFileNames`", ConsoleColor.Red);
                return 1;
            }

            settings.PrintSettingsInfo();

            Logger.Log("\nEnter 'y' to confirm these settings. Enter anything else to abort: ", ConsoleColor.White);
            string? input = Console.ReadLine();
            
            if (string.IsNullOrEmpty(input) || !input.Equals("y"))
            {
                Exit();
                return 0;
            }

            Logger.LogLine("\n> Packing & Compressing\n", ConsoleColor.White);
            BspPacker.PackFromConfig(settings);

            if (!settings.UploadMapsToWorkshop)
            {
                Logger.Close();
                Exit();
                return 0;
            }

            Logger.LogLine("> Uploading To Workshop\n", ConsoleColor.White);
            BspUploader.UploadMapsToWorkshop(settings, workshopMaps);


            Logger.Close();
            Exit();
            return 0;
        }

        public static int Exit(bool success = true)
        {
            Console.WriteLine("Press enter to continue...");
            Console.ReadLine();
            Environment.Exit(success ? 0 : 1);
            return success ? 0 : 1;
        }
    }
}
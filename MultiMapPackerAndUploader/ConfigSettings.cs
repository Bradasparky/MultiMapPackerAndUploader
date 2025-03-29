using Steamworks;
using System.Text.Json;

namespace MapPackerAndUploader
{
    public class BspConfig(string fileName)
    {
        public string FileName { get => fileName; }
        public IList<BspOptions> Options = [];
        public IList<string> SharedAssets = [];
    }

    class ConfigSettings
    {
        public string GameinfoPath { get; set; } = string.Empty;
        public string BspzipPath { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;
        public bool ForceMapCompression { get; init; } = false;
        public bool VerboseLogging { get; init; } = false;
        public bool UploadMapsToWorkshop { get; init; } = false;
        public HashSet<string> ExtensionWhitelist { get; set; } = [];
        public IList<string> BspConfigFileNames { get; set; } = [];
        public IList<BspConfig> BspConfigs = [];
        public IList<string> GlobalAssets = [];

        public void ParseConfigs()
        {
            if (!Directory.Exists("configs"))
            {
                Logger.LogLine("Failed to find the 'config' folder");
                Program.Exit(false);
            }

            Logger.LogLine("> Parsing Settings & Bsp Configs\n", ConsoleColor.White);

            foreach (var configName in BspConfigFileNames)
            {
                var configFilePath = "configs/" + configName + ".json";
                if (!File.Exists(configFilePath))
                {
                    Logger.LogLine($"Failed to find config file '{configFilePath}'", ConsoleColor.Red);
                    Program.Exit(false);
                }

                var jsonText = File.ReadAllText(configFilePath);
                JsonDocument doc;

                try
                {
                    doc = JsonDocument.Parse(jsonText);
                }
                catch (JsonException)
                {
                    Logger.LogLine($"Failed to parse '{configName}.json' due to invalid json formatting or value types", ConsoleColor.Red);
                    Program.Exit(false);
                    return;
                }
                catch (Exception ex)
                {
                    Logger.LogLine(ex.Message, ConsoleColor.Red);
                    Program.Exit(false);
                    return;
                }

                if (!doc.RootElement.TryGetProperty("maps", out JsonElement maps))
                {
                    Logger.LogLine($"Missing the 'maps' section in the config file '{configFilePath}'", ConsoleColor.Red);
                    Program.Exit(false);
                }

                var bspConfig = new BspConfig(configName);

                if (doc.RootElement.TryGetProperty("sharedAssets", out JsonElement assetArray))
                {
                    ParseAssets(assetArray, ref bspConfig.SharedAssets, configName + ".json");
                }

                ParseBspOptions(maps, ref bspConfig, configName);
                BspConfigs.Add(bspConfig);
            }
        }

        public void ParseBspOptions(JsonElement maps, ref BspConfig config, string configName)
        {
            var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var totalMaps = maps.GetArrayLength();

            for (var i = 0; i < totalMaps; i++)
            {
                JsonElement map = maps[i];
                BspOptions? options;

                try
                {
                    options = JsonSerializer.Deserialize<BspOptions>(map, serializerOptions);
                }
                catch (JsonException)
                {
                    Logger.LogLine($"Failed to parse '{configName}.json' due to invalid json or value types", ConsoleColor.Red);
                    Program.Exit(false);
                    return;
                }
                catch (Exception ex)
                {
                    Logger.LogLine(ex.Message, ConsoleColor.Red);
                    Program.Exit(false);
                    return;
                }

                if (options == null)
                {
                    Logger.LogLine($"Found a null map object within the config '{configName}.json'", ConsoleColor.Red);
                    Program.Exit(false);
                    return;
                }

                options.FileName = configName;

                if (string.IsNullOrEmpty(options.Name))
                {
                    Logger.LogLine($"Found a map without a proper name within the file '{config.FileName}'");
                    Program.Exit(false);
                }

                options.SourcePath = options.SourcePath.Replace('\\', '/');

                if (options.SourcePath.Equals(OutputPath))
                {
                    Logger.LogLine($"The value of 'sourcePath' for the bsp '{options.Name}' within '{config.FileName}' cannot be the same as the 'outputPath'", ConsoleColor.Red);
                    Program.Exit(false);
                }

                if (!File.Exists(options.SourcePath))
                {
                    Logger.LogLine($"The 'sourcePath' for the bsp '{options.Name}' within '{config.FileName}' does not exist", ConsoleColor.Red);
                    Program.Exit(false);
                }

                options.AbsOutputPath = OutputPath + options.Name + ".bsp";

                if (map.TryGetProperty("workshop", out JsonElement workshopSection))
                {
                    try
                    {
                        var nullCheck = JsonSerializer.Deserialize<WorkshopOptions>(workshopSection, serializerOptions);
                        if (nullCheck == null)
                        {
                            Logger.LogLine($"Found a null 'workshop' section for the bsp '{options.Name}' within '{config.FileName}'");
                            Program.Exit(false);
                            return;
                        }

                        options.Workshop = nullCheck;

                        if (options.Workshop != null)
                        {
                            ERemoteStoragePublishedFileVisibility fileVisibilityMin = ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic;
                            ERemoteStoragePublishedFileVisibility fileVisibilityMax = ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted;
                            if (options.Workshop.Visibility < fileVisibilityMin || options.Workshop.Visibility > fileVisibilityMax)
                            {
                                Logger.LogLine($"The map entry '{options.Name}' within '{config.FileName}' has an invalid 'visibility' value");
                                Program.Exit(false);
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        Logger.LogLine($"Failed to parse '{configName}.json' due to invalid json or value types for the bsp '{options.Name}'", ConsoleColor.Red);
                        Program.Exit(false);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogLine(ex.Message, ConsoleColor.Red);
                        Program.Exit(false);
                        return;
                    }
                }

                if (map.TryGetProperty("assets", out JsonElement assetsArray))
                {
                    ParseAssets(assetsArray, ref options.Assets, configName);
                }

                config.Options.Add(options);
            }
        }

        public void ParseAssets(JsonElement assetArray, ref IList<string> assetList, string configName)
        {
            var totalAssets = assetArray.GetArrayLength();

            for (var i = 0; i < totalAssets; i++)
            {
                string? assetPath = assetArray[i].GetString();

                if (string.IsNullOrEmpty(assetPath))
                {
                    Logger.LogLine($"Found a null or empty asset path '{assetPath}' within the `{configName}` config", ConsoleColor.Red);
                    Program.Exit(false);
                    return;
                }

                assetPath = assetPath.Replace('\\', '/');
                var fixedPath = assetPath.Replace("//", "/");
                var isValidFile = File.Exists(fixedPath);
                var isValidDirectory = Directory.Exists(fixedPath);

                if (!isValidDirectory && !isValidFile)
                {
                    Logger.LogLine($"Found an invalid asset path '{fixedPath}' within the `{configName}` config", ConsoleColor.Red);
                    Program.Exit(false);
                }

                var firstSlashes = assetPath.IndexOf("//");

                if (firstSlashes == -1)
                {
                    Logger.LogLine($"Found an invalid asset path '{fixedPath}' within the `{configName}` config.\nThe path must include a // or \\\\ that precedes a file/folder that you want to pack into the map", ConsoleColor.Red);
                    Program.Exit(false);
                }

                var finalSlashes = assetPath.LastIndexOf("//");

                if (firstSlashes != finalSlashes)
                {
                    Logger.LogLine($"Found an invalid asset path '{fixedPath}' within the `{configName}` config.\nThe path must not have more than two instances of // or \\\\", ConsoleColor.Red);
                    Program.Exit(false);
                }

                assetPath = assetPath.Remove(finalSlashes, 1);

                if (isValidFile)
                {
                    var internalPath = assetPath[(finalSlashes + 1)..];
                    assetList.Add(internalPath);
                    assetList.Add(assetPath);
                }
                else if (isValidDirectory)
                {
                    ParseDirectory(assetPath, firstSlashes + 1, ref assetList);
                }
            }
        }

        private void ParseDirectory(string assetPath, int doubleSlashesPos, ref IList<string> assetList)
        {
            try
            {
                var info = new DirectoryInfo(assetPath);
                var files = info.EnumerateFiles();

                foreach (var file in files)
                {
                    var fileAsString = file.ToString().Replace('\\', '/');
                    var fileExtension = fileAsString[fileAsString.LastIndexOf('/')..];
                    fileExtension = fileExtension[fileExtension.IndexOf('.')..];

                    if (ExtensionWhitelist.Count == 0 || ExtensionWhitelist.Contains(fileExtension))
                    {
                        assetList.Add(fileAsString[doubleSlashesPos..]);
                        assetList.Add(fileAsString);
                    }
                }

                var subdirectories = info.EnumerateDirectories();

                foreach (var subdirectory in subdirectories)
                {
                    ParseDirectory(subdirectory.ToString(), doubleSlashesPos, ref assetList);
                }
            }
            catch (Exception ex)
            {
                Logger.LogLine(ex.Message, ConsoleColor.Red);
                Program.Exit(false);
            }
        }

        public void PrintSettingsInfo()
        {
            // This is a mess but the result is pretty
            var configCount = 0;

            foreach (BspConfig config in BspConfigs)
            {
                char angleChar = configCount == 0 ? '╔' : '╠';
                Logger.LogLine($"{angleChar}══════════[ {config.FileName} ]", ConsoleColor.Green);
                Logger.AddPrefixToStack("║ ", ConsoleColor.Green);
                Logger.LogLine("");

                var optionCount = 0;
                var maxOptionCount = config.Options.Count;
                var totalIgnoredAssets = 0;

                foreach (var option in config.Options)
                {
                    if (!option.Enabled)
                    {
                        --maxOptionCount;
                        continue;
                    }

                    if (option.IgnoreAssets)
                    {
                        ++totalIgnoredAssets;
                    }

                    angleChar = optionCount == 0 ? '╔' : '╠';
                    Logger.LogLine($"{angleChar}══════════[ {option.Name} ]", ConsoleColor.Yellow);
                    Logger.AddPrefixToStack("║ ", ConsoleColor.Yellow);
                    Logger.LogLine("");
                    Logger.LogLine("Bsp Settings:", ConsoleColor.Yellow);
                    Logger.LogLine($"Source Path: {option.SourcePath}", ConsoleColor.Yellow);
                    Logger.LogLine($"Absolute Output Path: {option.AbsOutputPath}", ConsoleColor.Yellow);
                    Logger.LogLine($"Compress: {option.Compress}", ConsoleColor.Yellow);
                    Logger.LogLine($"Ignore Assets: {option.IgnoreAssets}", ConsoleColor.Yellow);

                    if (UploadMapsToWorkshop && option.Workshop.Upload)
                    {
                        var visibilityString = "";

                        switch (option.Workshop.Visibility)
                        {
                            case ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic:
                            {
                                visibilityString = "Public";
                                break;
                            }
                            case ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly:
                            {
                                visibilityString = "Friends Only";
                                break;
                            }
                            case ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate:
                            {
                                visibilityString = "Private";
                                break;
                            }
                            case ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted:
                            {
                                visibilityString = "Unlisted";
                                break;
                            }
                            default:
                            {
                                Logger.LogLine($"The 'workshop' section for the bsp '{option.Name}' within {config.FileName} has an invalid 'visibility' type");
                                Program.Exit(false);
                                break;
                            }
                        }

                        Logger.LogLine("Workshop Settings:", ConsoleColor.Yellow);
                        Logger.LogLine($"- ID: {option.Workshop.ID}", ConsoleColor.Yellow);
                        Logger.LogLine($"- Visibility: {visibilityString}", ConsoleColor.Yellow);
                        Logger.LogLine($"- Changelog: {option.Workshop.Changelog}", ConsoleColor.Yellow);
                        Logger.LogLine(" ");
                    }

                    if (option.Assets.Count > 0 && !option.IgnoreAssets)
                    {
                        if (VerboseLogging)
                        {
                            Logger.LogLine("");
                            Logger.LogLine("╔══════════[ Assets ]", ConsoleColor.Cyan);
                            Logger.AddPrefixToStack("║ ", ConsoleColor.Cyan);
                            Logger.LogLine("");

                            var assetCounter = 0;

                            foreach (var asset in option.Assets)
                            {
                                if (assetCounter++ % 2 == 0)
                                {
                                    continue;
                                }

                                var assetFileName = asset.Substring(asset.LastIndexOf('/') + 1);
                                Logger.Log(assetFileName, ConsoleColor.White);
                                Logger.Log(" >> ", ConsoleColor.Cyan, true);
                                Logger.LogLine(asset, ConsoleColor.White, true);
                            }

                            Logger.LogLine("");
                            Logger.RemovePrefixFromStack();
                            Logger.LogLine($"╚══════════[ Total: {option.Assets.Count / 2} ]", ConsoleColor.Cyan);
                            Logger.LogLine("");
                        }
                        else
                        {
                            Logger.LogLine("");
                            Logger.LogLine($"Total Assets: {option.Assets.Count / 2}", ConsoleColor.Cyan);
                            Logger.LogLine("");
                        }
                    }

                    Logger.RemovePrefixFromStack();
                    ++optionCount;
                }

                if (optionCount > 0)
                {
                    Logger.LogLine("╚]", ConsoleColor.Yellow);
                }

                if (totalIgnoredAssets < maxOptionCount)
                {
                    if (config.SharedAssets.Count > 0)
                    {
                        if (VerboseLogging)
                        {
                            Logger.LogLine("╠══════════[ Shared Assets ]", ConsoleColor.Green, true);
                            Logger.LogLine("");

                            var assetCounter = 1;

                            foreach (var asset in config.SharedAssets)
                            {
                                if (assetCounter++ % 2 == 1)
                                {
                                    continue;
                                }

                                var assetFileName = asset.Substring(asset.LastIndexOf('/') + 1);
                                Logger.Log(assetFileName, ConsoleColor.White);
                                Logger.Log(" >> ", ConsoleColor.Green, true);
                                Logger.LogLine(asset, ConsoleColor.White, true);
                            }
                        }
                    }

                    var totalSharedAssetsString = $"══════════[ Total Shared Assets: {config.SharedAssets.Count / 2} ]";
                    Logger.LogLine("");
                    Logger.LogLine(configCount < BspConfigs.Count - 1 ? $"╠{totalSharedAssetsString}" : $"╚{totalSharedAssetsString}", ConsoleColor.Green, true);
                }
                else
                {
                    if (configCount == BspConfigs.Count - 1)
                    {
                        Logger.LogLine("╚]", ConsoleColor.Green, true);
                    }
                }

                if (configCount < BspConfigs.Count - 1)
                {
                    Logger.LogLine("");
                }  

                Logger.RemovePrefixFromStack();

                ++configCount;
            }

            Logger.LogLine("");
            Logger.LogLine("╔══════════[ Settings ]", ConsoleColor.Cyan);
            Logger.AddPrefixToStack("║ ", ConsoleColor.Cyan);
            Logger.LogLine("");
            Logger.LogLine($"Bspzip Path: {BspzipPath}", ConsoleColor.Cyan);
            Logger.LogLine($"Output Path: {OutputPath}", ConsoleColor.Cyan);
            Logger.LogLine($"Force Map Compression: {ForceMapCompression}", ConsoleColor.Cyan);
            Logger.LogLine($"Upload Maps To Workshop: {UploadMapsToWorkshop}", ConsoleColor.Cyan);

            if (ExtensionWhitelist.Count > 0)
            {
                Logger.Log("Extension Whitelist: ", ConsoleColor.Cyan);

                var extCount = 0;

                foreach (var ext in ExtensionWhitelist)
                {
                    if (extCount < ExtensionWhitelist.Count - 1)
                    {
                        Logger.Log($"{ext}, ", ConsoleColor.Cyan, true);
                    }
                    else
                    {
                        Logger.LogLine($"{ext}", ConsoleColor.Cyan, true);
                    }

                    ++extCount;
                }
            }

            Logger.LogLine("");

            if (GlobalAssets.Count > 0)
            {
                if (VerboseLogging)
                {
                    Logger.LogLine("╠══════════[ Global Assets ]", ConsoleColor.Cyan, true);
                    Logger.LogLine("");

                    var assetCounter = 0;

                    foreach (var asset in GlobalAssets)
                    {
                        if (assetCounter++ % 2 == 1)
                        {
                            continue;
                        }

                        var assetFileName = asset.Substring(asset.LastIndexOf('/') + 1);
                        Logger.Log(assetFileName, ConsoleColor.White);
                        Logger.Log(" >> ", ConsoleColor.Cyan, true);
                        Logger.LogLine(asset, ConsoleColor.White, true);
                    }

                    Logger.LogLine("");
                    Logger.LogLine($"╚══════════[ Total Global Assets: {assetCounter} ]", ConsoleColor.Cyan, true);
                }
                else
                {
                    Logger.LogLine($"Total Global Assets: {GlobalAssets.Count / 2}", ConsoleColor.Cyan);
                    Logger.LogLine("");
                    Logger.LogLine($"╚]", ConsoleColor.Cyan, true);
                }
            }

            Logger.RemovePrefixFromStack();
        }
    }
}

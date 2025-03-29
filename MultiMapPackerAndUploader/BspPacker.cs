using System.Diagnostics;

namespace MapPackerAndUploader
{
    class BspPacker
    {
        public static void PackFromConfig(ConfigSettings settings)
        {
            var tempDir = (Path.GetTempPath() + "multi_map_packer_and_uploader/").Replace('\\', '/');

            try
            {
                Directory.CreateDirectory(tempDir);
            }
            catch (Exception ex)
            {
                Logger.LogLine(ex.Message, ConsoleColor.Red);
                Program.Exit(false);
            }

            if (!Directory.Exists(tempDir))
            {
                Logger.LogLine($"Failed to create temporary directory '{tempDir}'", ConsoleColor.Red);
                Program.Exit(false);
            }

            var tempAssetsFile = tempDir + "assets.txt";

            foreach (var config in settings.BspConfigs)
            {
                foreach (var option in config.Options)
                {
                    if (!option.Enabled)
                    {
                        continue;
                    }

                    var tempBsp = tempDir + option.Name + ".bsp";

                    try
                    {
                        File.Copy(option.SourcePath, tempBsp, true);
                    }
                    catch (Exception)
                    {
                        Directory.Delete(tempDir, true);
                        Logger.LogLine($"Failed to write a temp bsp in the temp directory '{tempDir}'", ConsoleColor.Red);
                        Program.Exit(false);
                    }

                    if (option.IgnoreAssets && !option.Compress)
                    {
                        // Copy it over manually
                        File.Copy(tempBsp, option.AbsOutputPath, true);
                        Logger.LogLine($"{option.Name} - No Operations Performed\n", ConsoleColor.Cyan);
                        continue;
                    }

                    var cmdGameinfo = $"-game \"{settings.GameinfoPath}\" ".Replace('\\', '/');
                    var cmdDecompress = cmdGameinfo + $"-repack \"{tempBsp}\"".Replace('\\', '/');

                    Logger.Log($"\r{option.Name} - Decompressing...{LotsOfSpaces()}", ConsoleColor.Yellow);
                    RunBspzipCommand(settings.BspzipPath, cmdDecompress);
                
                    if (!option.IgnoreAssets)
                    {
                        StreamWriter? stream;

                        try
                        {
                            stream = new StreamWriter(tempAssetsFile) { AutoFlush = true };
                        }
                        catch (Exception)
                        {
                            Directory.Delete(tempDir, true);
                            Logger.LogLine("", ConsoleColor.Red);
                            Logger.LogLine($"Failed to create temporary assets.txt file at '{tempDir}'", ConsoleColor.Red);
                            Program.Exit(false);
                            return;
                        }

                        foreach (var asset in option.Assets)
                        {
                            stream.WriteLine(asset);
                        }
                        
                        foreach (var asset in config.SharedAssets)
                        {
                            stream.WriteLine(asset);
                        }
                        
                        foreach (var asset in settings.GlobalAssets)
                        {
                            stream.WriteLine(asset);
                        }

                        stream.Close();
                        Logger.Log($"\r{option.Name} - Packing Assets...{LotsOfSpaces()}", ConsoleColor.Yellow);
                        var cmdPack = cmdGameinfo + $"-addlist \"{tempBsp}\" \"{tempAssetsFile}\" \"{option.AbsOutputPath}\"".Replace('\\', '/');
                        RunBspzipCommand(settings.BspzipPath, cmdPack);
                    }

                    if (option.Compress || settings.ForceMapCompression)
                    {
                        Logger.Log($"\r{option.Name} - Compressing...{LotsOfSpaces()}", ConsoleColor.Yellow);
                        var cmdCompress = cmdGameinfo + $"-repack \"{tempBsp}\"".Replace('\\', '/');
                        RunBspzipCommand(settings.BspzipPath, cmdCompress);
                    }

                    // It needs to be copied over manually because we didn't run the -addlist command to do it for us
                    if (option.IgnoreAssets)
                    {
                        File.Copy(tempBsp, option.AbsOutputPath, true);
                    }

                    Logger.LogLine($"\r{option.Name} - Completed!{LotsOfSpaces()}\n", ConsoleColor.Cyan);
                }
            }

            Directory.Delete(tempDir, true);
            Process.Start(new ProcessStartInfo()
            {
                FileName = settings.OutputPath,
                UseShellExecute = true,
                Verb = "open"
            });
        }

        public static string LotsOfSpaces()
        {
            return "                                        ";
        }

        public static int RunBspzipCommand(string bspzipPath, string args)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = bspzipPath,
                    Arguments = args,
                    UseShellExecute = true,
                    Verb = "open",
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };
            process.Start();
            process.WaitForExit();
            return process.ExitCode;
        }
    }
}

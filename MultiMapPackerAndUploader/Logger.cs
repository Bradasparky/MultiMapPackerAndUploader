namespace MapPackerAndUploader
{
    public class Logger
    {
        private static StreamWriter? LogStream;
        private static IList<(string, ConsoleColor)> PrefixStack = [];
        public static StreamWriter Initialize()
        {
            var logDir = Directory.GetCurrentDirectory() + "/logs/";

            if (!Directory.Exists(logDir))
            {
                try
                {
                    Directory.CreateDirectory(logDir);
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed to create a 'logs' folder in the working directory");
                    Program.Exit(false);
                }
            }

            LogStream = new StreamWriter(logDir + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            LogStream.AutoFlush = true;
            return LogStream;
        }

        public static void Log(string text, ConsoleColor color = ConsoleColor.Gray, bool ignorePrefix = false)
        {
            LogStream ??= Initialize();

            if (!ignorePrefix && PrefixStack.Count > 0)
            {
                foreach (var prefix in PrefixStack)
                {
                    LogStream.Write(prefix.Item1);
                    ColoredConsole.Write(prefix.Item1, prefix.Item2);
                }

                LogStream.Write(text);
                ColoredConsole.Write(text, color);
            }
            else
            {
                LogStream.Write(text);
                ColoredConsole.Write(text, color);
            }
        }
        
        public static void LogLine(string text, ConsoleColor color = ConsoleColor.Gray, bool ignorePrefix = false)
        {
            LogStream ??= Initialize();

            if (!ignorePrefix && PrefixStack.Count > 0)
            {
                foreach (var prefix in PrefixStack)
                {
                    LogStream.Write(prefix.Item1);
                    ColoredConsole.Write(prefix.Item1, prefix.Item2);
                }

                LogStream.WriteLine(text);
                ColoredConsole.WriteLine(text, color);
            }
            else
            {
                LogStream.WriteLine(text);
                ColoredConsole.WriteLine(text, color);
            }
        }

        public static void AddPrefixToStack(string text, ConsoleColor color = ConsoleColor.Gray)
        {
            PrefixStack.Add((text, color));
        }
        
        public static void RemovePrefixFromStack()
        {
            if (PrefixStack.Count == 0)
            {
                return;
            }

            PrefixStack.RemoveAt(PrefixStack.Count - 1);
        }

        public static void Close()
        {
            LogStream?.Close();
            LogStream = null;
        }
    }
}

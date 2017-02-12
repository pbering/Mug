using System;

namespace Mug
{
    public class ConsoleLogger : ILogger
    {
        private static readonly object _consoleLock = new object();

        public void WriteLine(LogLevel level, string format, params object[] arg)
        {
            lock (_consoleLock)
            {
                var foregroundColor = Console.ForegroundColor;

                switch (level)
                {
                    case LogLevel.Dbg:
                        foregroundColor = ConsoleColor.Magenta;

                        break;
                    case LogLevel.Wrn:
                        foregroundColor = ConsoleColor.Yellow;

                        break;
                    case LogLevel.Err:
                        foregroundColor = ConsoleColor.Red;

                        break;
                }

                var message = DateTime.Now.ToLongTimeString() + ": " + format;

                Console.ForegroundColor = foregroundColor;
                Console.Write("[" + level.ToString().ToUpper() + "] ");
                Console.WriteLine(message, arg);
                Console.ResetColor();
            }
        }
    }
}
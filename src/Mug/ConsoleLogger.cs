using System;

namespace Mug
{
    public class ConsoleLogger : ILogger
    {
        public void WriteLine(string format, params object[] arg)
        {
            var message = DateTime.Now.ToLongTimeString() + ": " + format;

            Console.WriteLine(message, arg);
        }
    }
}
namespace Mug
{
    public interface ILogger
    {
        void WriteLine(LogLevel level, string format, params object[] arg);
    }

    public enum LogLevel
    {
        Dbg = 0,
        Nfo = 1,
        Wrn = 2,
        Err = 3
    }
}
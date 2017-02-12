namespace Mug
{
    public interface ILogger
    {
        void WriteLine(string format, params object[] arg);
    }
}
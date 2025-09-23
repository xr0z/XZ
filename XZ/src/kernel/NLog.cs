using NLog;
namespace XZ;

public static class Log
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    internal static void Info(string str)
    {
        logger.Info(str);
    }
    internal static void Warn(string str)
    {
        logger.Warn(str);
    }
    internal static void Error(string str)
    {
        logger.Error(str);
    }
    internal static void Fatal(string str)
    {
        logger.Fatal(str);
    }
}
using System.Threading.Tasks;

namespace XZ;

internal static class OnExit
{
    internal static async Task Shutdown(UserDatabase db)
    {
        db.Save();
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("Goodbye - XZ System");
        Log.Info("Shutdown successfly - :)");
        NLog.LogManager.Shutdown();
        await Task.Delay(1000);
        Console.Clear();
        Environment.Exit(0);
    }
    internal static void Errordown(UserDatabase db)
    {
        db.Save();
        Console.ForegroundColor = ConsoleColor.Blue;
        Log.Fatal("Errordown - Unexpected Error has occured by unknown reason");
        NLog.LogManager.Shutdown();
        Environment.Exit(1);
    }
    internal static async Task Logout(UserDatabase db)
    {
        db.Save();
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Blue;
        Log.Info("Logout from user successfly");
        await OnStart.Start(db);
    }
    internal static void AuthLimit(UserDatabase db)
    {
        db.Save();
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Blue;
        Log.Error("You reached the limit of auth...");
    }
}
internal class ExitManager
{
    internal static Dictionary<string, Action> ExitReasons;

    internal static void InitializeExitReasons(UserDatabase db)
    {
        ExitReasons = new Dictionary<string, Action>
        {
            ["shutdown"] = async () => await OnExit.Shutdown(db),
            ["logout"] = async () => await OnExit.Logout(db),
            ["authlimit"] = () => OnExit.AuthLimit(db),
            ["unknown"] = () => OnExit.Errordown(db)
        };
    }
}
namespace XZ;

internal static class OnUser
{
    internal static async Task<string> Home(UserDatabase db)
    {
        await Task.Delay(50);
        Console.WriteLine("\n");
        Console.Clear();
        Log.Info("Login successfly");
        Console.ForegroundColor = ConsoleColor.Green;
        CommandHandler cmd = new();
        dbStorage.db = db;
        CommandHandler commandHandler = new();
        while (true)
        {
            string cmdinput = Mod.Input("XZ_System_1.0Beta");
            await commandHandler.Call(cmdinput);
        }
        /*
        if (isShutdown())
        {
            return "shutdown";
        }
        else
        {
            return "unknown";
        }
        */
    }
    internal static bool isShutdown()
    {
        string yn = Mod.Input("Do you really want to Shutdown? (y/n)");
        return getYn(yn);
    }
    internal static bool isLogout()
    {
        string yn = Mod.Input("Do you really want to Logout? (y/n)");
        return getYn(yn);
    }
    internal static bool getYn(string input)
    {
        switch (input)
        {
            case "y" or "Y":
                return true;
            case "n" or "N":
                return false;
            default:
                Console.WriteLine("Please type yes or no :(");
                return false;
        }
    }

}
internal sealed class Session
{
    public User CurrentUser { get; }
    public DateTime CreatedAt { get; }
    public Guid SessionId { get; }

    public Session(User user)
    {
        CurrentUser = user;
        CreatedAt = DateTime.UtcNow;
        SessionId = Guid.NewGuid();
    }
}

internal static class SessionManager
{
    private static Session? _current;

    public static Session Current =>
        _current ?? throw new InvalidOperationException("ログインされていません");

    public static bool IsActive => _current != null;

    public static void Start(User user)
    {
        if (_current != null)
            throw new InvalidOperationException("既にログイン中です");

        _current = new Session(user);
    }

    public static void End()
    {
        if (_current == null)
            throw new InvalidOperationException("セッションは存在しません");

        _current = null;
    }
}


using System.Net.Http.Headers;

namespace XZ;

internal static class OnStart
{
    internal static async Task Start(UserDatabase db)
{
    string exeDir = Path.GetDirectoryName(Environment.ProcessPath)!;
        Environment.CurrentDirectory = exeDir;
        Console.WriteLine(Environment.CurrentDirectory);
    bool isLoginSuccess = false;
    User? loggedInUser = null;

    for (int loginLimits = 1; loginLimits <= 10; loginLimits++)
    {
        loggedInUser = Login(db);
        if (loggedInUser != null)
        {
            isLoginSuccess = true;
            SessionManager.Start(loggedInUser);
            break;
        }
    }

    if (!isLoginSuccess)
    {
        ExitManager.ExitReasons["authlimit"]();
        return;
    }

    ExitManager.InitializeExitReasons(db);
    string exitReason = await OnUser.Home(db);
    ExitManager.ExitReasons[exitReason]();
}
    internal static User? Login(UserDatabase db)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        string username = Mod.Input("\nUsername");
        string password = ReadPassword();

        bool v = db.VerifyUser(username, password);
        if (!v) return null;

        return db.Users.First(u => u.Username == username);
    }
    private static string ReadPassword()
    {
        Console.Write("Password > ");
        //セキュリティを高めるためパスワード入力中はその文字が表示されません
        string pass = string.Empty;
        ConsoleKeyInfo key;

        while (true)
        {
            key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }
            else if (key.Key == ConsoleKey.Backspace)
            {
                if (pass.Length > 0)
                {
                    pass = pass.Substring(0, pass.Length - 1);
                }
            }
            else
            {
                pass += key.KeyChar;
            }
        }
        return pass;
    }
}

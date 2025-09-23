using System.Reflection;
using System.Diagnostics;
namespace XZ;

internal static class DirectoryData
{
    internal static string GetCurD()
    {
        string currentPath = Directory.GetCurrentDirectory();
        string directoryName = new DirectoryInfo(currentPath).Name;
        return directoryName;
    }
}

internal class CommandHandler
{
    internal Dictionary<string, Func<string[], Task<string>>> CommandList;
    internal CommandHandler()
    {
        CommandData cmdData = new();
        CommandList = new Dictionary<string, Func<string[], Task<string>>>(20000)
        {
            {"help", cmdData.Help},
            {"shutdown", cmdData.Shutdown},
            {"logout", cmdData.Logout},
            {"adduser", cmdData.AddUser}
        };
        LoadCommandsFromFolder("./Mods");
    }
    private void LoadCommandsFromFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Console.WriteLine($"[WARN] Folder '{folderPath}' does not exist.");
            return;
        }

        foreach (var file in Directory.GetFiles(folderPath, "*.dll"))
        {
            try
            {
                Assembly assembly = Assembly.LoadFrom(file);
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(ICommand).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        ICommand commandInstance = (ICommand)Activator.CreateInstance(type);
                        CommandList[commandInstance.Name] = commandInstance.Execute;
                        Log.Info($"Command '{commandInstance.Name}' loaded from '{file}'.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load commands from '{file}': {ex.Message}");
            }
        }
    }

    internal async Task<bool> IsExist(string command)
    {
        try
        {
            if (CommandList.ContainsKey(command) && !string.IsNullOrEmpty(command))
            {
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {

            Log.Fatal(ex.ToString());
            return false;
        }

    }
    internal async Task<string> Call(string command)
    {

        string[] args = command.Split(" ");
        string cmdName = args[0];
        bool isExist = await IsExist(cmdName);
        if (isExist)
        {
            Task<string> cmd = CommandList[cmdName](args.Skip(1).ToArray());
            string result = await cmd;
            switch (result)
            {
                case "ok":
                    break;
                case "err":
                    Log.Error("Command Execute Error");
                    break;
                case "crit":
                default:
                    Log.Fatal("Critical Command Execute Error");
                    break;
            }
            return result;
        }
        else if (command == "")
        {
            return "ok";
        }
        else
        {
            Log.Error($"Unknown command : {command}");
            return "err";
        }
    }
}

internal class CommandData
{
    internal async Task<string> Help(string[] args)
    {
        try
        {
            Console.Write("XZ System 1.0-beta.1 \n[-- Author : ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("AARR Dev / Advanced Army of Red Raider ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(" --]\n1. help - view help command\n2. testmod - only for test modifyapp\nSorry i will add others");
            return "ok";
        }
        catch (Exception ex)
        {

            Log.Info(ex.ToString());
            return "err";
        }
    }
    internal async Task<string> Run(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                Log.Warn("No data to run...");
                return "err";
            }
            RunApplication r = new();
            await r.Run(args[0], false);
            return "ok";
        }
        catch (Exception ex)
        {
            Log.Error(ex.ToString());
            return "err";
        }
    }
    internal async Task<string> Shutdown(string[] args)
    {
        if (OnUser.isShutdown())
        {
            await OnExit.Shutdown(db: dbStorage.db);
            return "ok";
        }
        else
        {
            return "ok";
        }

    }
    internal async Task<string> Logout(string[] args)
    {
        if (OnUser.isLogout())
        {
            await OnExit.Logout(db: dbStorage.db);
            return "ok";
        }
        else
        {
            return "ok";
        }
    }
    internal async Task<string> AddUser(string[] args)
    {
        if (SessionManager.Current.CurrentUser.IsAdmin)
        {
            Log.Info("XZ System User Manager - Add User");
            string username = Mod.Input("New user's name");
            string password = Mod.Input("New user's password");
            bool admin = OnUser.getYn(Mod.Input("Grant Admin access to new user? (y/n)"));
            dbStorage.db.AddUser(username, password, admin);
            dbStorage.db.Save();
            return "ok";
        }
        else
        {
            Log.Error("You need admin to execute this command.");
            return "err";
        }
    }
}
internal class RunApplication
{
    internal async Task<bool> Run(string filename, bool createwindow)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = filename,
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = createwindow
            };

            using var process = Process.Start(psi);
            process.WaitForExit();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
internal static class dbStorage
{
    internal static UserDatabase db;
}

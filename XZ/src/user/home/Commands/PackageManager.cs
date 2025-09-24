using System.Net.Http;
using System.Text.Json;
using System.Diagnostics;
using System.IO.Compression;

internal class PackageManager
{
    private readonly string installDbPath = "./data/pkg_installed.json";
    private Dictionary<string, string> installedPkgs = new();
    private HttpClient http = new HttpClient();

    public PackageManager()
    {
        LoadInstalled();
    }

    private void LoadInstalled()
    {
        if (File.Exists(installDbPath))
        {
            string json = File.ReadAllText(installDbPath);
            installedPkgs = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                            ?? new Dictionary<string, string>();
        }
    }

    private void SaveInstalled()
    {
        string json = JsonSerializer.Serialize(installedPkgs, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(Path.GetDirectoryName(installDbPath)!);
        File.WriteAllText(installDbPath, json);
    }

    public async Task<string> Pkg(string[] args)
    {
        if (args.Length == 0) return "err";
        string sub = args[0];

        switch (sub)
        {
            case "install":
                if (args.Length < 2) return "err";
                return await Install(args[1]);
            case "remove":
                if (args.Length < 2) return "err";
                return Remove(args[1]);
            case "list":
                foreach (var kv in installedPkgs)
                    Console.WriteLine($"{kv.Key} -> {kv.Value}");
                return "ok";
            default:
                Console.WriteLine("Usage: pkg [install|remove|list] <name>");
                return "err";
        }
    }

    private async Task<string> Install(string name)
    {
        string pkgListUrl = "https://raw.githubusercontent.com/xr0z/XZ_PkgList/refs/heads/main/packages.json";
        http.DefaultRequestHeaders.UserAgent.ParseAdd("pkg-manager");

        string json = await http.GetStringAsync(pkgListUrl);
        var remotePkgs = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

        if (remotePkgs == null || !remotePkgs.ContainsKey(name))
        {
            Console.WriteLine($"Package {name} not found.");
            return "err";
        }

        string repoUrl = remotePkgs[name];
        string apiUrl = repoUrl.Replace("https://github.com/", "https://api.github.com/repos/") + "/releases/latest";

        string relJson = await http.GetStringAsync(apiUrl);
        using var doc = JsonDocument.Parse(relJson);
        var asset = doc.RootElement.GetProperty("assets")[0];
        string assetUrl = asset.GetProperty("browser_download_url").GetString()!;
        string assetName = asset.GetProperty("name").GetString()!;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string binDir = Path.Combine(home, ".mypkgs", "bin");
        string cacheDir = Path.Combine(home, ".mypkgs", "cache");
        Directory.CreateDirectory(binDir);
        Directory.CreateDirectory(cacheDir);

        string cachePath = Path.Combine(cacheDir, assetName);

        Console.WriteLine($"Downloading {assetUrl}...");
        byte[] fileData = await http.GetByteArrayAsync(assetUrl);
        await File.WriteAllBytesAsync(cachePath, fileData);

        string targetPath = Path.Combine(binDir, name);

        if (assetName.EndsWith(".zip"))
        {
            string extractDir = Path.Combine(cacheDir, name + "_extract");
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            ZipFile.ExtractToDirectory(cachePath, extractDir);

            string? exeFile = Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories)
                .FirstOrDefault(f =>
                    Path.GetFileName(f).Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(f).Equals(name + ".exe", StringComparison.OrdinalIgnoreCase));

            if (exeFile == null)
            {
                Console.WriteLine("No matching executable found in zip.");
                return "err";
            }

            File.Copy(exeFile, targetPath, true);
        }
        else
        {
            File.Copy(cachePath, targetPath, true);
        }

        if (!OperatingSystem.IsWindows())
        {
            Process.Start("chmod", $"+x {targetPath}")?.WaitForExit();
        }

        installedPkgs[name] = targetPath;
        SaveInstalled();

        Console.WriteLine($"Installed {name} to {targetPath}");
        return "ok";
    }

    private string Remove(string name)
    {
        if (!installedPkgs.ContainsKey(name))
            return "err";

        string path = installedPkgs[name];
        if (File.Exists(path)) File.Delete(path);
        installedPkgs.Remove(name);
        SaveInstalled();

        Console.WriteLine($"Removed {name}");
        return "ok";
    }

    public async Task<string> Run(string[] args)
    {
        if (args.Length == 0) return "err";
        string name = args[0];

        if (!installedPkgs.ContainsKey(name))
        {
            Console.WriteLine($"Not installed: {name}");
            return "err";
        }

        string exePath = installedPkgs[name];
        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true
        });

        return "ok";
    }

}

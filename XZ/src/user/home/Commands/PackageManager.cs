using System.Net.Http;
using System.Text.Json;
using System.Diagnostics;
using System.IO.Compression;

internal class PackageManager
{
    private readonly string installDbPath = "./data/pkg_installed.json";
    private Dictionary<string, string> installedPkgs = new();
    private readonly HttpClient http = new();

    public PackageManager()
    {
        LoadInstalled();
    }

    private void LoadInstalled()
    {
        try
        {
            if (File.Exists(installDbPath))
            {
                string json = File.ReadAllText(installDbPath);
                installedPkgs = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                                ?? new Dictionary<string, string>();
            }
        }
        catch
        {
            Console.WriteLine("Warning: installed package DB is corrupted. Reinitializing.");
            installedPkgs = new();
        }
    }

    private void SaveInstalled()
    {
        try
        {
            string json = JsonSerializer.Serialize(installedPkgs, new JsonSerializerOptions { WriteIndented = true });
            string? dir = Path.GetDirectoryName(installDbPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(installDbPath, json);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to save installed DB: {e.Message}");
        }
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

        string json;
        try
        {
            json = await http.GetStringAsync(pkgListUrl);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to download package list: {e.Message}");
            return "err";
        }

        Dictionary<string, string>? remotePkgs = null;
        try
        {
            remotePkgs = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to parse package list: {e.Message}");
            return "err";
        }

        if (remotePkgs == null || !remotePkgs.ContainsKey(name))
        {
            Console.WriteLine($"Package {name} not found in list.");
            return "err";
        }

        string repoUrl = remotePkgs[name];
        string apiUrl = repoUrl.Replace("https://github.com/", "https://api.github.com/repos/") + "/releases/latest";

        string relJson;
        try
        {
            relJson = await http.GetStringAsync(apiUrl);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to fetch release info: {e.Message}");
            return "err";
        }

        using var doc = JsonDocument.Parse(relJson);
        if (!doc.RootElement.TryGetProperty("assets", out var assets) || assets.GetArrayLength() == 0)
        {
            Console.WriteLine("No assets found in the latest release.");
            return "err";
        }

        var asset = assets[0];
        string? assetUrl = asset.GetProperty("browser_download_url").GetString();
        string? assetName = asset.GetProperty("name").GetString();

        if (assetUrl == null || assetName == null)
        {
            Console.WriteLine("Invalid asset data.");
            return "err";
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string binDir = Path.Combine(home, ".mypkgs", "bin");
        string cacheDir = Path.Combine(home, ".mypkgs", "cache");
        Directory.CreateDirectory(binDir);
        Directory.CreateDirectory(cacheDir);

        string cachePath = Path.Combine(cacheDir, assetName);
        Console.WriteLine($"Downloading {assetUrl}...");

        try
        {
            byte[] fileData = await http.GetByteArrayAsync(assetUrl);
            await File.WriteAllBytesAsync(cachePath, fileData);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Download failed: {e.Message}");
            return "err";
        }

        string targetPath = Path.Combine(binDir, name);
        if (File.Exists(targetPath))
            File.Delete(targetPath);

        try
        {
            if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
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
                var chmod = new ProcessStartInfo("chmod", $"+x \"{targetPath}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                using var proc = Process.Start(chmod);
                proc?.WaitForExit();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Install error: {e.Message}");
            return "err";
        }

        installedPkgs[name] = targetPath;
        SaveInstalled();

        Console.WriteLine($"Installed {name} to {targetPath}");
        return "ok";
    }

    private string Remove(string name)
    {
        if (!installedPkgs.ContainsKey(name))
        {
            Console.WriteLine($"Package not installed: {name}");
            return "err";
        }

        string path = installedPkgs[name];
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to delete {name}: {e.Message}");
            return "err";
        }

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
        if (!File.Exists(exePath))
        {
            Console.WriteLine($"Executable not found: {exePath}");
            return "err";
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.GetFullPath(exePath),
                UseShellExecute = true
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to run {name}: {e.Message}");
            return "err";
        }

        return "ok";
    }
}

using System.Net.Http;
using System.Text.Json;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

internal class PackageManager
{
    private readonly string installDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "pkg_installed.json");
    private Dictionary<string, string> installedPkgs = new();
    
    private static readonly HttpClient http = new();

    public PackageManager()
    {
        if (http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("pkg-manager-csharp");
        }
        LoadInstalled();
    }

    private void LoadInstalled()
    {
        try
        {
            if (File.Exists(installDbPath))
            {
                string json = File.ReadAllText(installDbPath);
                installedPkgs = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
        }
        catch
        {
            Console.WriteLine("Warning: installed package DB is corrupted.");
            installedPkgs = new();
        }
    }

    private void SaveInstalled()
    {
        try
        {
            string? dir = Path.GetDirectoryName(installDbPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            
            string json = JsonSerializer.Serialize(installedPkgs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(installDbPath, json);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to save DB: {e.Message}");
        }
    }

    public async Task<string> ExecuteCommand(string[] args)
    {
        if (args.Length == 0) return "usage";
        string sub = args[0].ToLower();

        return sub switch
        {
            "install" => args.Length > 1 ? await Install(args[1]) : "missing name",
            "remove"  => args.Length > 1 ? Remove(args[1]) : "missing name",
            "list"    => List(),
            "run"     => args.Length > 1 ? await Run(args.Skip(1).ToArray()) : "missing name",
            _         => "unknown command"
        };
    }

    private string List()
    {
        if (installedPkgs.Count == 0) Console.WriteLine("No packages installed.");
        foreach (var kv in installedPkgs) Console.WriteLine($"{kv.Key} -> {kv.Value}");
        return "ok";
    }

    private async Task<string> Install(string name)
    {
        // 1. パッケージリストの取得（実際の実装ではURLを定数化推奨）
        string pkgListUrl = "https://raw.githubusercontent.com/xr0z/XZ_PkgList/main/packages.json";
        
        var response = await http.GetAsync(pkgListUrl);
        if (!response.IsSuccessStatusCode) return "pkg list not found";

        var remotePkgs = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(await response.Content.ReadAsStreamAsync());
        if (remotePkgs == null || !remotePkgs.TryGetValue(name, out var repoUrl))
        {
            Console.WriteLine($"Package {name} not found.");
            return "err";
        }

        string apiUrl = repoUrl.Replace("https://github.com/", "https://api.github.com/repos/") + "/releases/latest";
        var relResponse = await http.GetAsync(apiUrl);
        if (!relResponse.IsSuccessStatusCode) return "api error";

        using var doc = JsonDocument.Parse(await relResponse.Content.ReadAsStringAsync());
        var assets = doc.RootElement.GetProperty("assets").EnumerateArray();

        var asset = assets.FirstOrDefault(a => 
            a.GetProperty("name").GetString()!.Contains(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" : "linux", StringComparison.OrdinalIgnoreCase)
            || a.GetProperty("name").GetString()!.EndsWith(".zip") 
        );

        if (asset.ValueKind == JsonValueKind.Undefined) asset = assets.First();

        string assetUrl = asset.GetProperty("browser_download_url").GetString()!;
        string assetName = asset.GetProperty("name").GetString()!;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string binDir = Path.Combine(home, ".mypkgs", "bin");
        string cacheDir = Path.Combine(home, ".mypkgs", "cache");
        Directory.CreateDirectory(binDir);
        Directory.CreateDirectory(cacheDir);

        string cachePath = Path.Combine(cacheDir, assetName);
        Console.WriteLine($"Downloading {assetName}...");
        var data = await http.GetByteArrayAsync(assetUrl);
        await File.WriteAllBytesAsync(cachePath, data);

        string targetPath = Path.Combine(binDir, name + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : ""));
        
        try
        {
            if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                string extractDir = Path.Combine(cacheDir, name + "_temp");
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                ZipFile.ExtractToDirectory(cachePath, extractDir);

                var exeFile = Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories)
                    .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase));

                if (exeFile != null)
                {
                    File.Copy(exeFile, targetPath, true);
                }
                Directory.Delete(extractDir, true); // 後片付け
            }
            else
            {
                File.Copy(cachePath, targetPath, true);
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("chmod", $"+x \"{targetPath}\"")?.WaitForExit();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Install error: {e.Message}");
            return "err";
        }

        installedPkgs[name] = targetPath;
        SaveInstalled();
        Console.WriteLine($"Successfully installed {name}");
        return "ok";
    }

    public string Remove(string name)
    {
        if (installedPkgs.TryGetValue(name, out var path))
        {
            if (File.Exists(path)) File.Delete(path);
            installedPkgs.Remove(name);
            SaveInstalled();
            Console.WriteLine($"Removed {name}");
            return "ok";
        }
        return "not installed";
    }

    public async Task<string> Run(string[] args)
    {
        string name = args[0];
        if (!installedPkgs.TryGetValue(name, out var exePath)) return "not installed";

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = string.Join(" ", args.Skip(1)), 
            UseShellExecute = false, 
            CreateNoWindow = false
        };

        try
        {
            using var proc = Process.Start(startInfo);
            if (proc != null) await proc.WaitForExitAsync();
            return "ok";
        }
        catch (Exception e)
        {
            return $"run error: {e.Message}";
        }
    }
}
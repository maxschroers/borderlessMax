using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace BorderlessMax;

// Exe and Title differ from Path only for games that share their exe (Minecraft Java: javaw.exe, Path is the launcher)
public record AppEntry(string Name, string Path, string Source)
{
    public string Exe { get; init; } = System.IO.Path.GetFileName(Path);
    public string? Title { get; init; }
    public string Detail => $"{Source} · {Exe}";
    public ImageSource? Icon => Apps.Icon(Path);
}

// Finds the programs and games on this PC for the picker: running apps, game launchers, Start menu, installed apps.
static partial class Apps
{
    // installers, crash reporters, anti-cheat and other helpers that are never the game window
    [GeneratedRegex(@"^unins|uninst|crash|setup|updat|redist|install|helper|report|prereq|easyanticheat|^eac|battleye|cefprocess|webview|dotnet|cleanup|repair|vc_?redist|dxweb",RegexOptions.IgnoreCase)]
    private static partial Regex Junk();

    // also everything under C:\Windows: system tools and the installer cache's ARPPRODUCTICON.exe
    static readonly string Windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\";

    public static bool IsJunk(string exe) =>
        Junk().IsMatch(Path.GetFileNameWithoutExtension(exe)) || exe.StartsWith(Windows, StringComparison.OrdinalIgnoreCase);

    public static List<AppEntry> Scan()
    {
        var found = new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase);
        void Add(string name, string path, string source)
        {
            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !IsJunk(path) && !found.ContainsKey(path) && File.Exists(path))
                found[path] = new(name, path, source);
        }
        // each source on its own: a missing launcher or an unreadable folder must not hide the rest
        foreach (var source in new Action<Action<string, string, string>>[] { Running, Steam, Epic, Xbox, Riot, StartMenu, Installed })
            try { source(Add); } catch { }
        try { if (MinecraftJava() is { } mc) found["minecraft-java"] = mc; } catch { }
        return found.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    static void Running(Action<string, string, string> add)
    {
        foreach (var p in Process.GetProcesses())
            using (p)
                try
                {
                    if (p.MainWindowHandle == IntPtr.Zero || p.MainWindowTitle == "") continue;
                    var path = p.MainModule!.FileName;
                    add(Describe(path), path, L.I["running"]);
                }
                catch { } // elevated or exited processes
    }

    static void Steam(Action<string, string, string> add)
    {
        if (Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") is not string steam) return;
        foreach (var lib in SteamLibraries(File.ReadAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"))))
            foreach (var acf in SafeFiles(Path.Combine(lib, "steamapps"), "appmanifest_*.acf"))
            {
                var text = File.ReadAllText(acf);
                var name = VdfValue(text, "name");
                var dir = VdfValue(text, "installdir");
                if (name is null || dir is null) continue;
                foreach (var exe in Exes(Path.Combine(lib, "steamapps", "common", dir), 4)) // Unreal: Game\Binaries\Win64, Source 2: game\bin\win64
                    add(name, exe, "Steam");
            }
    }

    public static IEnumerable<string> SteamLibraries(string vdf) =>
        Regex.Matches(vdf, "\"path\"\\s+\"(.+?)\"").Select(m => m.Groups[1].Value.Replace(@"\\", @"\"));

    static string? VdfValue(string vdf, string key) => Regex.Match(vdf, $"\"{key}\"\\s+\"(.+?)\"") is { Success: true } m ? m.Groups[1].Value : null;

    static void Epic(Action<string, string, string> add)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Epic\EpicGamesLauncher\Data\Manifests");
        foreach (var item in SafeFiles(dir, "*.item"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(item));
            var r = doc.RootElement;
            add(r.GetProperty("DisplayName").GetString()!,
                Path.Combine(r.GetProperty("InstallLocation").GetString()!, r.GetProperty("LaunchExecutable").GetString()!), "Epic");
        }
    }

    static void Xbox(Action<string, string, string> add)
    {
        foreach (var drive in FixedDrives())
            foreach (var game in SafeDirs(Path.Combine(drive, "XboxGames")))
                foreach (var exe in Exes(Path.Combine(game, "Content"), 2))
                    add(Path.GetFileName(game), exe, "Xbox");
    }

    // the Riot Client notes every install in ProgramData\Riot Games\Metadata\*\*.product_settings.yaml (any folder, e.g.
    // C:\Games\Riot Games\VALORANT\live); the default <drive>:\Riot Games folders are checked as well
    static void Riot(Action<string, string, string> add)
    {
        var metadata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Riot Games\Metadata");
        var games = SafeDirs(metadata).SelectMany(d => SafeFiles(d, "*.product_settings.yaml"))
            .Select(f => RiotInstallPath(File.ReadAllText(f))).OfType<string>()
            .Concat(FixedDrives().SelectMany(d => SafeDirs(Path.Combine(d, "Riot Games"))));
        foreach (var game in games)
            foreach (var exe in Exes(game, 4)) // VALORANT: live\ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe
                add(RiotName(game), exe, "Riot");
    }

    public static string? RiotInstallPath(string yaml) =>
        Regex.Match(yaml, "product_install_full_path:\\s*\"(.+?)\"") is { Success: true } m ? Path.GetFullPath(m.Groups[1].Value) : null;

    // "...\VALORANT\live" -> "VALORANT", "...\Teamfight Tactics\Live" -> "Teamfight Tactics"
    public static string RiotName(string dir)
    {
        var name = Path.GetFileName(dir.TrimEnd('\\', '/'));
        return name.Equals("live", StringComparison.OrdinalIgnoreCase) || name.Equals("pbe", StringComparison.OrdinalIgnoreCase)
            ? $"{Path.GetFileName(Path.GetDirectoryName(dir.TrimEnd('\\', '/')))}{(name.Equals("pbe", StringComparison.OrdinalIgnoreCase) ? " (PBE)" : "")}"
            : name;
    }

    // every launcher (official, Prism, Modrinth, CurseForge) starts the game as javaw.exe with a "Minecraft* 1.21.4 - ..." window;
    // offered when one of their data folders exists, with the official launcher's icon if it is installed
    static AppEntry? MinecraftJava()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!new[] { Path.Combine(appData, ".minecraft"), Path.Combine(appData, "PrismLauncher"), Path.Combine(appData, "ModrinthApp"),
                Path.Combine(home, "curseforge", "minecraft") }.Any(Directory.Exists)) return null;
        var launcher = FixedDrives().Select(d => Path.Combine(d, @"XboxGames\Minecraft Launcher\Content\Minecraft.exe"))
            .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Minecraft Launcher\MinecraftLauncher.exe"))
            .FirstOrDefault(File.Exists);
        return new("Minecraft: Java Edition", launcher ?? "", "Minecraft") { Exe = "javaw.exe", Title = "Minecraft" };
    }

    static void StartMenu(Action<string, string, string> add)
    {
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        foreach (var folder in new[] { Environment.SpecialFolder.CommonPrograms, Environment.SpecialFolder.Programs })
            foreach (var lnk in Directory.EnumerateFiles(Environment.GetFolderPath(folder), "*.lnk", SearchOption.AllDirectories))
                try { add(Path.GetFileNameWithoutExtension(lnk), (string)shell.CreateShortcut(lnk).TargetPath, L.I["start_menu"]); }
                catch { }
    }

    static void Installed(Action<string, string, string> add)
    {
        const string key = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using var uninstall = RegistryKey.OpenBaseKey(root == Registry.LocalMachine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, view).OpenSubKey(key);
                foreach (var sub in uninstall?.GetSubKeyNames() ?? [])
                {
                    using var app = uninstall!.OpenSubKey(sub);
                    if (app?.GetValue("DisplayName") is not string name) continue;
                    // DisplayIcon is often the main exe ("C:\...\app.exe",0); otherwise look at the top of the install folder
                    var icon = (app.GetValue("DisplayIcon") as string)?.Split(',')[0].Trim('"', ' ');
                    if (icon?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true && !IsJunk(icon))
                        add(name, icon, L.I["installed"]);
                    else if (app.GetValue("InstallLocation") is string dir && dir != "")
                        foreach (var exe in Exes(dir.Trim('"'), 1))
                            add(name, exe, L.I["installed"]);
                }
            }
    }

    static IEnumerable<string> FixedDrives() =>
        DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).Select(d => d.RootDirectory.FullName);

    // exes in a folder and its subfolders, `depth` levels including the folder itself
    static IEnumerable<string> Exes(string dir, int depth) =>
        SafeFiles(dir, "*.exe").Concat(depth > 1 ? SafeDirs(dir).SelectMany(d => Exes(d, depth - 1)) : []);

    static IEnumerable<string> SafeFiles(string dir, string pattern)
    {
        try { return Directory.GetFiles(dir, pattern); } catch { return []; }
    }

    static IEnumerable<string> SafeDirs(string dir)
    {
        try { return Directory.GetDirectories(dir); } catch { return []; }
    }

    // "Elden Ring" from the exe's version info, else the file name
    public static string Describe(string path)
    {
        try { if (FileVersionInfo.GetVersionInfo(path).FileDescription is { Length: > 0 } d) return d.Trim(); } catch { }
        return Path.GetFileNameWithoutExtension(path);
    }

    static readonly Dictionary<string, ImageSource?> icons = new(StringComparer.OrdinalIgnoreCase);
    public static readonly string[] ImageTypes = [".png", ".jpg", ".jpeg", ".ico", ".bmp", ".gif"];

    // an image file as it is, anything else (exe) by its associated icon
    public static ImageSource? Icon(string path)
    {
        if (icons.TryGetValue(path, out var cached)) return cached;
        try
        {
            if (ImageTypes.Contains(Path.GetExtension(path).ToLowerInvariant()))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path);
                bmp.DecodePixelWidth = 64; // shown at 32 px, sharp on 200 % scaling
                bmp.CacheOption = BitmapCacheOption.OnLoad; // file not kept open
                bmp.EndInit();
                bmp.Freeze();
                return icons[path] = bmp;
            }
            using var ic = System.Drawing.Icon.ExtractAssociatedIcon(path)!;
            var img = Imaging.CreateBitmapSourceFromHIcon(ic.Handle, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            img.Freeze();
            return icons[path] = img;
        }
        catch { return icons[path] = null; }
    }
}

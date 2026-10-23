using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;

namespace BorderlessMax;

// The portable exe installs itself per user (no admin) and, once installed, updates itself from GitHub releases.
static class Installer
{
    public const string Repo = "maxschroers/borderlessMax";
    public const string Asset = App.Name + "-windows.exe"; // release asset name (.github/workflows/release.yml)
    public static readonly string Version =
        typeof(Installer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev";

    static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", App.Name);
    public static readonly string Exe = Path.Combine(Dir, App.Name + ".exe");
    static readonly string NewExe = Exe + ".new", OldExe = Exe + ".old";
    static readonly string Lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), App.Name + ".lnk");
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + App.Name;
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool Installed => string.Equals(Environment.ProcessPath, Exe, StringComparison.OrdinalIgnoreCase);

    public static void Install()
    {
        Directory.CreateDirectory(Dir);
        File.Copy(Environment.ProcessPath!, Exe, true);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        var lnk = shell.CreateShortcut(Lnk);
        lnk.TargetPath = Exe;
        lnk.Save();
        Register();
        if (Autostart) SetAutostart(true, Exe); // point the Run entry at the installed copy
    }

    // entry in Settings → Apps; its uninstall button runs `borderlessMax.exe --uninstall`
    static void Register()
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
        foreach (var (name, value) in new[] {
            ("DisplayName", App.Name), ("DisplayVersion", Version), ("Publisher", "Max Schroers"), ("DisplayIcon", Exe),
            ("InstallLocation", Dir), ("URLInfoAbout", $"https://github.com/{Repo}"), ("UninstallString", $"\"{Exe}\" --uninstall") })
            key.SetValue(name, value);
        key.SetValue("NoModify", 1);
        key.SetValue("NoRepair", 1);
    }

    // a running exe can't delete itself: a detached cmd removes the folder once this process has exited
    public static void Uninstall()
    {
        File.Delete(Lnk);
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
        SetAutostart(false);
        Process.Start(new ProcessStartInfo("cmd", $"/c ping -n 5 127.0.0.1 >nul & rmdir /s /q \"{Dir}\"")
            { CreateNoWindow = true, UseShellExecute = false });
    }

    public static bool Autostart => Registry.CurrentUser.OpenSubKey(RunKey)?.GetValue(App.Name) is not null;

    public static void SetAutostart(bool on, string? exe = null)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) key.SetValue(App.Name, $"\"{exe ?? Environment.ProcessPath}\" --tray");
        else key.DeleteValue(App.Name, false);
    }

    public static void CleanUp()
    {
        try { File.Delete(OldExe); } catch { } // left by the last update, the old process held it
    }

    public record Update(string Version, string? Url, long Size);

    // GitHub's latest release, null when there is nothing newer or anything fails (offline, rate-limited, odd tag)
    public static async Task<Update?> Check()
    {
        if (!System.Version.TryParse(Version, out _)) return null; // dev build
        try
        {
            using var http = Http();
            using var doc = JsonDocument.Parse(await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest"));
            var latest = doc.RootElement.GetProperty("tag_name").GetString()!.TrimStart('v');
            if (!Newer(latest, Version)) return null;
            var asset = doc.RootElement.GetProperty("assets").EnumerateArray().FirstOrDefault(a => a.GetProperty("name").GetString() == Asset);
            return asset.ValueKind == JsonValueKind.Undefined || !Installed
                ? new(latest, null, 0) // portable: the release page
                : new(latest, asset.GetProperty("browser_download_url").GetString(), asset.GetProperty("size").GetInt64());
        }
        catch { return null; }
    }

    public static bool Newer(string latest, string current) =>
        System.Version.TryParse(latest, out var a) && System.Version.TryParse(current, out var b) && a > b;

    // Windows can rename a running exe, not overwrite it: swap the files, the caller restarts into the new one
    public static async Task Apply(Update update, IProgress<int> progress)
    {
        using var http = Http();
        http.Timeout = TimeSpan.FromMinutes(5);
        using var reply = await http.GetAsync(update.Url, HttpCompletionOption.ResponseHeadersRead);
        reply.EnsureSuccessStatusCode();
        var data = new MemoryStream();
        await using (var body = await reply.Content.ReadAsStreamAsync())
        {
            var buffer = new byte[81920];
            for (int n; (n = await body.ReadAsync(buffer)) > 0;)
            {
                data.Write(buffer, 0, n);
                progress.Report((int)(data.Length * 100 / Math.Max(1, update.Size)));
            }
        }
        var bytes = data.ToArray();
        if (bytes.Length != update.Size || bytes is not [(byte)'M', (byte)'Z', ..]) throw new IOException("incomplete download");
        File.WriteAllBytes(NewExe, bytes);
        try
        {
            File.Move(Exe, OldExe, true);
            File.Move(NewExe, Exe);
        }
        catch
        {
            if (!File.Exists(Exe) && File.Exists(OldExe)) File.Move(OldExe, Exe); // swap half done: put the running version back
            throw;
        }
        Register(); // new version in Settings → Apps
    }

    static HttpClient Http()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"{App.Name}/{Version}"); // GitHub requires one
        return http;
    }

    public static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}

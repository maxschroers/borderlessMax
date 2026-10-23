using System.Drawing;

namespace BorderlessMax;

// `borderlessMax.exe --selftest`: logic checks, exit code 0 = all passed, otherwise the number of the failed check.
// CI runs it before every build (.github/workflows/release.yml).
static class SelfTest
{
    public static int Run()
    {
        var ultrawide = new Rectangle(0, 0, 5120, 1440);
        var checks = new Func<bool>[]
        {
            // 1-4: centering
            () => Helper.Center(ultrawide, 2560, 1440) == new Rectangle(1280, 0, 2560, 1440),
            () => Helper.Center(ultrawide, 1920, 1080) == new Rectangle(1600, 180, 1920, 1080),
            () => Helper.Center(new(-2560, -200, 2560, 1440), 1920, 1080) == new Rectangle(-2240, -20, 1920, 1080), // monitor left of primary
            () => Helper.Center(ultrawide, 9999, 9999) == ultrawide, // oversize clamps to monitor
            // 5-7: versions compare as numbers, dev builds never update
            () => Installer.Newer("0.10.0", "0.9.3") && !Installer.Newer("0.9.3", "0.10.0"),
            () => !Installer.Newer("1.0.0", "1.0.0"),
            () => !Installer.Newer("1.0.0", "dev"),
            // 8-9: settings survive a save / load round trip, old or hand-written files still load
            () =>
            {
                var s = new Settings([new("Elden Ring", "eldenring.exe", 2560, 1440, false, @"C:\Games\eldenring.exe")], TaskbarScope.AllScreens, false, "de");
                var back = Settings.Parse(s.ToJson());
                return back.ToJson() == s.ToJson() && back.Games[0] == s.Games[0] && back.Taskbar == TaskbarScope.AllScreens;
            },
            () => Settings.Parse("""{ "games": [ { "exe": "x.exe", "name": "X", "width": 1920, "height": 1080 } ], }""") is
                { Taskbar: TaskbarScope.GameScreen, Stretch: true, Mode: TaskbarMode.Behind, Games: [{ Enabled: true, Path: null }] }
                && Settings.Parse("""{ "games": [], "mode": "Hide" }""").Mode == TaskbarMode.Hide,
            // 10: Steam library paths from libraryfolders.vdf
            () => Apps.SteamLibraries("""
                "libraryfolders" { "0" { "path"		"C:\\Program Files (x86)\\Steam" } "1" { "path"		"D:\\SteamLibrary" } }
                """).SequenceEqual([@"C:\Program Files (x86)\Steam", @"D:\SteamLibrary"]),
            // 11-12: helper exes are dropped, games stay
            () => new[] { "unins000.exe", "UnityCrashHandler64.exe", "vc_redist.x64.exe", "EasyAntiCheat_EOS_Setup.exe", "gamelaunchhelper.exe",
                @"C:\Windows\Installer\{1234}\ARPPRODUCTICON.exe" }.All(Apps.IsJunk),
            () => !new[] { "eldenring.exe", "League of Legends.exe", "Cyberpunk2077.exe", "Game-Win64-Shipping.exe", "javaw.exe" }.Any(Apps.IsJunk),
            // 13: picker search: every word in the name, the exe or the source
            () => AppPicker.Matches(new("ELDEN RING", @"C:\x\eldenring.exe", "Steam"), "elden ring")
                && AppPicker.Matches(new("ELDEN RING", @"C:\x\eldenring.exe", "Steam"), "ELDENRING.EXE")
                && AppPicker.Matches(new("ELDEN RING", @"C:\x\eldenring.exe", "Steam"), "steam elden")
                && !AppPicker.Matches(new("ELDEN RING", @"C:\x\eldenring.exe", "Steam"), "elden lord"),
            // 14: default size of a new game: 16:9 at screen height
            () => Displays.DefaultSize(5120, 1440) == (2560, 1440) && Displays.DefaultSize(1280, 1024) == (1280, 1024),
            // 15-16: display modes: tiny ones and duplicates go, largest first, all screens named
            () => Displays.Build([("S1", 5120, 1440), ("S1", 2560, 1440), ("S2", 2560, 1440), ("S1", 640, 400), ("S1", 1920, 1080), ("S1", 2560, 1440)])
                .Select(r => (r.Width, r.Height, r.Info)).SequenceEqual([(5120, 1440, "32:9 · S1"), (2560, 1440, "16:9 · S1, S2"), (1920, 1080, "16:9 · S1")]),
            () => Displays.Aspect(2560, 1080) == "21:9" && Displays.Aspect(3440, 1440) == "21:9" && Displays.Aspect(1680, 1050) == "16:10"
                && Displays.Aspect(1366, 768) == "16:9" && Displays.Aspect(1280, 1024) == "5:4",
            // 17: both languages have the same texts
            () => L.Keys("de").Order().SequenceEqual(L.Keys("en").Order()),
            // 18-19: games without own size follow the default; own sizes win; tiny sizes are raised
            () =>
            {
                var s = Settings.Parse("""{ "games": [ { "exe": "a.exe", "name": "A" }, { "exe": "b.exe", "name": "B", "width": 1920, "height": 1080 } ], "defaultWidth": 2560, "defaultHeight": 1440 }""");
                return s.SizeOf(s.Games[0]) == (2560, 1440) && s.SizeOf(s.Games[1]) == (1920, 1080)
                    && (s with { DefaultWidth = 100, DefaultHeight = 50 }).SizeOf(s.Games[0]) == (320, 200);
            },
            () => !new Settings([new("A", "a.exe")]).ToJson().Contains("Width\": null"), // unset sizes stay out of the file
            // 20: Riot installs from the client's metadata, named after the game folder
            () => Apps.RiotInstallPath("product_install_full_path: \"C:/Games/Riot Games/VALORANT/live\"\nother: 1") == @"C:\Games\Riot Games\VALORANT\live"
                && Apps.RiotName(@"C:\Games\Riot Games\VALORANT\live") == "VALORANT"
                && Apps.RiotName(@"C:\Riot Games\Teamfight Tactics\PBE") == "Teamfight Tactics (PBE)"
                && Apps.RiotName(@"C:\Games\Riot Games\League of Legends") == "League of Legends"
                && !Apps.IsJunk("VALORANT-Win64-Shipping.exe"),
        };
        for (int i = 0; i < checks.Length; i++)
            if (!checks[i]()) return i + 1;
        return 0;
    }
}

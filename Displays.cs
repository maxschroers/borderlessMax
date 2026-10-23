using static BorderlessMax.Native;

namespace BorderlessMax;

public record Resolution(int Width, int Height, string Info)
{
    public static readonly Resolution Custom = new(0, 0, "");
    public bool IsCustom => Width == 0;
    public string Size => IsCustom ? L.I["custom"] : $"{Width}x{Height}";
}

// Resolutions the displays of this PC really offer, for the per-game dropdown.
static class Displays
{
    static List<Resolution>? modes;
    public static List<Resolution> Modes => modes ??= Build(Query());

    static IEnumerable<(string Screen, int W, int H)> Query()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        for (int s = 0; s < screens.Length; s++)
        {
            var dm = new DEVMODE { dmSize = (ushort)System.Runtime.InteropServices.Marshal.SizeOf<DEVMODE>() };
            for (int i = 0; EnumDisplaySettings(screens[s].DeviceName, i, ref dm); i++)
                yield return (L.I.Format("screen", s + 1), (int)dm.dmPelsWidth, (int)dm.dmPelsHeight);
        }
    }

    public static List<Resolution> Build(IEnumerable<(string Screen, int W, int H)> raw) =>
        raw.Where(m => m.W >= 640 && m.H >= 480)
            .GroupBy(m => (m.W, m.H))
            .OrderByDescending(g => g.Key.W * g.Key.H).ThenByDescending(g => g.Key.W)
            .Select(g => new Resolution(g.Key.W, g.Key.H, $"{Aspect(g.Key.W, g.Key.H)} · {string.Join(", ", g.Select(m => m.Screen).Distinct())}"))
            .ToList();

    // nearest common name: 2560x1080 is "21:9", not 64:27
    public static string Aspect(int w, int h)
    {
        var known = new[] { (32, 9), (21, 9), (16, 9), (16, 10), (3, 2), (4, 3), (5, 4) };
        var (a, b) = known.MinBy(k => Math.Abs((double)k.Item1 / k.Item2 - (double)w / h));
        return $"{a}:{b}";
    }

    // new games: 16:9 at the height of the main screen, so the game fills it top to bottom (2560x1440 on 5120x1440)
    public static (int W, int H) DefaultSize(int screenW, int screenH) => (Math.Min(screenW, screenH * 16 / 9), screenH);
}

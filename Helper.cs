using System.Diagnostics;
using System.Drawing;
using static BorderlessMax.Native;

namespace BorderlessMax;

// The actual job: while a configured game has focus, keep it borderless and centered and the taskbar out of the way.
static class Helper
{
    public static Settings Settings = Settings.Load();
    public static event Action<Game?>? ActiveChanged;
    public static Game? Active { get; private set; }
    static bool taskbarHidden;
    static uint? savedTaskbarState; // auto-hide setting to restore when the taskbar comes back

    public static void Tick()
    {
        var hwnd = GetForegroundWindow();
        var exe = ExeName(hwnd);
        var game = exe is null ? null : Settings.Games.FirstOrDefault(g => g.Enabled && g.Exe.Equals(exe, StringComparison.OrdinalIgnoreCase));
        if (game is null || IsIconic(hwnd))
        {
            // tabbed out, minimized or closed
            ShowTaskbar();
            SetActive(null);
            return;
        }

        SetActive(game);
        ApplyBorderless(hwnd, game);
        HideTaskbar(hwnd);
    }

    static void SetActive(Game? game)
    {
        if (game == Active) return;
        Active = game;
        ActiveChanged?.Invoke(game);
    }

    static string? ExeName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return null;
        GetWindowThreadProcessId(hwnd, out uint pid);
        try { using var p = Process.GetProcessById((int)pid); return p.ProcessName + ".exe"; }
        catch { return null; }
    }

    static void ApplyBorderless(IntPtr hwnd, Game game)
    {
        const long frame = WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
        const long exFrame = WS_EX_DLGMODALFRAME | WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE | WS_EX_STATICEDGE;
        long style = GetWindowLongPtr(hwnd, GWL_STYLE), ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        bool styleChanged = (style & frame) != 0 || (style & WS_POPUP) == 0 || (ex & exFrame) != 0;
        if (styleChanged)
        {
            SetWindowLongPtr(hwnd, GWL_STYLE, (style & ~frame) | WS_POPUP);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, ex & ~exFrame);
        }

        var m = MonitorOf(hwnd).rcMonitor;
        var (w, h) = Settings.SizeOf(game);
        var target = Center(Rectangle.FromLTRB(m.Left, m.Top, m.Right, m.Bottom), w, h);

        GetWindowRect(hwnd, out var r);
        if (styleChanged || Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom) != target)
            SetWindowPos(hwnd, HWND_TOP, target.X, target.Y, target.Width, target.Height,
                SWP_FRAMECHANGED | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    static MONITORINFO MonitorOf(IntPtr hwnd)
    {
        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref info);
        return info;
    }

    public static Rectangle Center(Rectangle monitor, int w, int h)
    {
        w = Math.Min(w, monitor.Width);
        h = Math.Min(h, monitor.Height);
        return new(monitor.X + (monitor.Width - w) / 2, monitor.Y + (monitor.Height - h) / 2, w, h);
    }

    // Primary taskbar plus one per extra monitor; looked up each time because Explorer restarts change the handles.
    static IEnumerable<IntPtr> Taskbars()
    {
        foreach (var cls in new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
            for (var h = FindWindowEx(IntPtr.Zero, IntPtr.Zero, cls, null); h != IntPtr.Zero; h = FindWindowEx(IntPtr.Zero, h, cls, null))
                yield return h;
    }

    static void HideTaskbar(IntPtr game)
    {
        // re-checked every tick so the taskbar is hidden again if Explorer brings it back
        var screen = MonitorFromWindow(game, MONITOR_DEFAULTTONEAREST);
        foreach (var h in Taskbars())
            if (IsWindowVisible(h) && (Settings.Taskbar == TaskbarScope.AllScreens || MonitorFromWindow(h, MONITOR_DEFAULTTONEAREST) == screen))
                ShowWindow(h, SW_HIDE);

        // a hidden taskbar still reserves its space and Windows ignores SPI_SETWORKAREA while it is an appbar;
        // auto-hide frees the space (on every monitor, it's one setting) and Windows stretches maximized windows itself
        if (Settings.Stretch)
        {
            var state = TaskbarState(ABM_GETSTATE, 0);
            savedTaskbarState ??= state;
            if ((state & ABS_AUTOHIDE) == 0) TaskbarState(ABM_SETSTATE, state | ABS_AUTOHIDE);
        }
        taskbarHidden = true;
    }

    public static void ShowTaskbar()
    {
        if (!taskbarHidden) return;
        if (savedTaskbarState is uint state)
        {
            TaskbarState(ABM_SETSTATE, state);
            savedTaskbarState = null;
        }
        foreach (var h in Taskbars()) ShowWindow(h, SW_SHOW);
        taskbarHidden = false;
    }

    static uint TaskbarState(uint msg, uint state)
    {
        var data = new APPBARDATA { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<APPBARDATA>(), lParam = (IntPtr)state };
        return (uint)SHAppBarMessage(msg, ref data);
    }
}

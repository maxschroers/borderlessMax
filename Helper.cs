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
    static bool taskbarHidden; // or pushed behind the game: something to undo when the game loses focus
    static uint? savedTaskbarState; // auto-hide setting to restore when the taskbar comes back
    static IntPtr marked; // game window marked full-screen for the taskbar (TaskbarMode.Behind)
    static ITaskbarList2? taskbarList;

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
        if (game.KeepFrame) PlaceAboveTaskbar(hwnd);
        else ApplyBorderless(hwnd, game);
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

    // frame and size stay as the game wants them; only the visible window is moved so its bottom touches the taskbar
    static void PlaceAboveTaskbar(IntPtr hwnd)
    {
        if (IsZoomed(hwnd)) return;
        GetWindowRect(hwnd, out var r);
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var v, System.Runtime.InteropServices.Marshal.SizeOf<RECT>()) != 0) v = r;
        var w = MonitorOf(hwnd).rcWork;
        var target = AboveTaskbar(Rectangle.FromLTRB(w.Left, w.Top, w.Right, w.Bottom), Rectangle.FromLTRB(v.Left, v.Top, v.Right, v.Bottom));
        int x = target.X + r.Left - v.Left, y = target.Y + r.Top - v.Top; // add the invisible borders back
        if (x != r.Left || y != r.Top)
            SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    // centered, bottom on the work area's bottom (= top of a bottom taskbar); too tall: the title bar stays on screen
    public static Rectangle AboveTaskbar(Rectangle work, Rectangle window) =>
        new(work.X + (work.Width - window.Width) / 2, Math.Max(work.Top, work.Bottom - window.Height), window.Width, window.Height);

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
        if (Settings.Mode == TaskbarMode.Behind)
        {
            // Windows only puts the taskbar behind windows that cover the whole screen; marked ones count as such
            if (marked != game)
            {
                Unmark();
                (taskbarList ??= TaskbarList()).MarkFullscreenWindow(game, true);
                marked = game;
            }
        }
        else
        {
            // re-checked every tick so the taskbar is hidden again if Explorer brings it back
            var screen = MonitorFromWindow(game, MONITOR_DEFAULTTONEAREST);
            foreach (var h in Taskbars())
                if (IsWindowVisible(h) && (Settings.Taskbar == TaskbarScope.AllScreens || MonitorFromWindow(h, MONITOR_DEFAULTTONEAREST) == screen))
                    ShowWindow(h, SW_HIDE);
        }

        // a hidden taskbar still reserves its space and Windows ignores SPI_SETWORKAREA while it is an appbar;
        // auto-hide frees the space (on every monitor, it's one setting) and Windows stretches maximized windows itself
        if (Settings.Stretch && Settings.Mode == TaskbarMode.Hide) // behind the game the taskbar keeps its space
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
        Unmark(); // the taskbar comes back in front of the game when it loses focus
        foreach (var h in Taskbars()) ShowWindow(h, SW_SHOW);
        taskbarHidden = false;
    }

    static void Unmark()
    {
        if (marked == IntPtr.Zero) return;
        try { taskbarList?.MarkFullscreenWindow(marked, false); } catch { } // the game may be gone already
        marked = IntPtr.Zero;
    }

    static uint TaskbarState(uint msg, uint state)
    {
        var data = new APPBARDATA { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<APPBARDATA>(), lParam = (IntPtr)state };
        return (uint)SHAppBarMessage(msg, ref data);
    }
}

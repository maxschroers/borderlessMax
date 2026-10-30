using System.Runtime.InteropServices;

namespace BorderlessMax;

static partial class Native
{
    public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    public const int SW_HIDE = 0, SW_SHOW = 5;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const uint ABM_GETSTATE = 0x4, ABM_SETSTATE = 0xA, ABS_AUTOHIDE = 0x1;

    public const long WS_POPUP = 0x80000000, WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000,
        WS_SYSMENU = 0x00080000, WS_MINIMIZEBOX = 0x00020000, WS_MAXIMIZEBOX = 0x00010000;
    public const long WS_EX_DLGMODALFRAME = 0x1, WS_EX_WINDOWEDGE = 0x100, WS_EX_CLIENTEDGE = 0x200,
        WS_EX_STATICEDGE = 0x20000;

    public const uint SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20, SWP_NOOWNERZORDER = 0x200;
    public static readonly IntPtr HWND_TOP = IntPtr.Zero;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage, uEdge; public RECT rc; public IntPtr lParam; }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DEVMODE
    {
        public fixed char dmDeviceName[32];
        public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public uint dmFields;
        public int dmPositionX, dmPositionY;
        public uint dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        public fixed char dmFormName[32];
        public ushort dmLogPixels;
        public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency,
            dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    // the shell's taskbar list: a window marked full-screen is drawn in front of the taskbar while it is active
    [ComImport, Guid("602D4995-B13A-429b-A66E-1935E44F4317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ITaskbarList2
    {
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
    }

    public static ITaskbarList2 TaskbarList()
    {
        var list = (ITaskbarList2)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("56FDF344-FD6D-11d0-958A-006097C9A090"))!)!;
        list.HrInit();
        return list;
    }

    [LibraryImport("user32.dll")] public static partial IntPtr GetForegroundWindow();
    [LibraryImport("user32.dll")] public static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int GetWindowText(IntPtr hWnd, [Out] char[] text, int max);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool IsIconic(IntPtr hWnd);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool IsWindowVisible(IntPtr hWnd);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool ShowWindow(IntPtr hWnd, int cmd);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool GetWindowRect(IntPtr hWnd, out RECT r);
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static partial long GetWindowLongPtr(IntPtr hWnd, int index);
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static partial long SetWindowLongPtr(IntPtr hWnd, int index, long value);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int w, int h, uint flags);
    [LibraryImport("user32.dll")] public static partial IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")] [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);
    [LibraryImport("shell32.dll")] public static partial IntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [LibraryImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumDisplaySettings(string device, int mode, ref DEVMODE dm);
    [LibraryImport("dwmapi.dll")] public static partial int DwmSetWindowAttribute(IntPtr hWnd, int attr, ref int value, int size);
}

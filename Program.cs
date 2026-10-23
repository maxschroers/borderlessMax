using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace BorderlessMax;

static class App
{
    public const string Name = "borderlessMax";
}

static class Program
{
    static Application app = null!;
    static MainWindow? window;
    static System.Windows.Forms.NotifyIcon tray = null!;

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--selftest")) return SelfTest.Run();

        using var mutex = new Mutex(false, App.Name);
        using var show = new EventWaitHandle(false, EventResetMode.AutoReset, App.Name + ".show");
        using var exit = new EventWaitHandle(false, EventResetMode.AutoReset, App.Name + ".exit");

        app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/borderlessMax;component/Theme.xaml", UriKind.Relative)));
        L.I.Lang = Helper.Settings.Lang ?? L.Default;

        if (args.Contains("--uninstall")) // from Settings → Apps
        {
            if (!Installer.Installed || !Dialog.Ask(null, L.I["uninstall_ask"])) return 0;
            exit.Set(); // the running copy restores the taskbar and quits, then its exe can be deleted
            Acquire(mutex, 10000);
            Installer.Uninstall();
            return 0;
        }

        // one copy only: a second start opens the first one's window; after install / update wait for the old one to quit
        if (!Acquire(mutex, args.Contains("--restart") ? 10000 : 0))
        {
            show.Set();
            return 0;
        }
        Installer.CleanUp();

        app.DispatcherUnhandledException += (_, e) => Fail(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, _) => Helper.ShowTaskbar();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Helper.ShowTaskbar();
        app.SessionEnding += (_, _) => Helper.ShowTaskbar();
        ThreadPool.RegisterWaitForSingleObject(show, (_, _) => app.Dispatcher.BeginInvoke(ShowWindow), null, -1, false);
        ThreadPool.RegisterWaitForSingleObject(exit, (_, _) => app.Dispatcher.BeginInvoke(Quit), null, -1, false);

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(L.I["open"], null, (_, _) => ShowWindow());
        menu.Items.Add(L.I["exit"], null, (_, _) => Quit());
        tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!), Text = App.Name, ContextMenuStrip = menu, Visible = true,
        };
        tray.MouseClick += (_, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Left) ShowWindow(); };
        tray.BalloonTipClicked += (_, _) => ShowWindow();
        Helper.ActiveChanged += g => tray.Text = g is null ? App.Name : $"{App.Name}: {g.Exe}";

        // ponytail: 500 ms polling, switch to SetWinEventHook if the lag is noticeable
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) => Helper.Tick();
        timer.Start();

        if (!args.Contains("--tray")) ShowWindow(); // --tray: started with Windows
        _ = CheckUpdate();
        app.Run();

        Helper.ShowTaskbar();
        tray.Dispose();
        return 0;
    }

    static bool Acquire(Mutex mutex, int ms)
    {
        try { return mutex.WaitOne(ms); }
        catch (AbandonedMutexException) { return true; } // the old copy was killed: it's ours now
    }

    static async Task CheckUpdate()
    {
        if (await Installer.Check() is not { } update) return;
        window ??= new MainWindow();
        window.ShowUpdate(update);
        if (!window.IsVisible) tray.ShowBalloonTip(5000, App.Name, L.I.Format("update", update.Version), System.Windows.Forms.ToolTipIcon.Info);
    }

    static void ShowWindow()
    {
        window ??= new MainWindow();
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }

    public static void Quit()
    {
        Helper.ShowTaskbar();
        if (window is not null)
        {
            window.Quitting = true;
            window.Close();
        }
        app.Shutdown();
    }

    // after install / update: start the new exe, which waits for this one to quit (--restart)
    public static void Restart(string exe)
    {
        Process.Start(exe, "--restart");
        Quit();
    }

    static void Fail(Exception e)
    {
        Helper.ShowTaskbar();
        Dialog.Tell(null, $"{L.I["crashed"]}\n\n{e}");
        Quit();
    }
}

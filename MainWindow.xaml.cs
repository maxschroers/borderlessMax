using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BorderlessMax;

public record LangItem(string Code, string Name, ImageSource Flag);

public class GameRow(Game game) : INotifyPropertyChanged
{
    public string Name { get; } = game.Name;
    public string Exe { get; } = game.Exe;
    public string? Path { get; } = game.Path;
    public ImageSource? Icon => Path is null ? null : Apps.Icon(Path);
    int width = game.Width, height = game.Height;
    bool enabled = game.Enabled, custom;

    public int Width { get => width; set { width = value; Changed(); } }
    public int Height { get => height; set { height = value; Changed(); } }
    public bool Enabled { get => enabled; set { enabled = value; Changed(); } }

    // the matching display mode, or "Custom…" (then the width / height fields show)
    public Resolution Selected
    {
        get => custom ? Resolution.Custom : Displays.Modes.FirstOrDefault(m => m.Width == width && m.Height == height) ?? Resolution.Custom;
        set
        {
            if (value is null) return;
            custom = value.IsCustom;
            if (!custom) (width, height) = (value.Width, value.Height);
            Changed();
        }
    }
    public bool IsCustom => Selected.IsCustom;

    public Game ToGame() => new(Name, Exe, width, height, enabled, Path);

    public event PropertyChangedEventHandler? PropertyChanged;
    void Changed() => PropertyChanged?.Invoke(this, new("")); // "" = every property
}

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<GameRow> Rows { get; } = [];
    public List<Resolution> Resolutions { get; } = [.. Displays.Modes, Resolution.Custom];
    public List<LangItem> LangItems { get; } =
        L.Languages.Select(l => new LangItem(l.Key, l.Value, (ImageSource)Application.Current.FindResource("Flag_" + l.Key))).ToList();
    public string VersionText { get; } = Installer.Version == "dev" ? "dev" : "v" + Installer.Version;

    int scope;
    bool stretch, autostart;
    string saved = "";
    Installer.Update? update;
    public bool Quitting;

    public MainWindow()
    {
        InitializeComponent();
        Icon = Dialog.Icon;
        DataContext = this;
        SourceInitialized += (_, _) => Dialog.DarkTitleBar(this);
        Rows.CollectionChanged += (_, _) => Notify(nameof(NoGames));
        Helper.ActiveChanged += _ => Notify(nameof(StatusText), nameof(StatusBrush));
        Load();
        if (!Installer.Installed) ShowAction = true; // portable: offer the install
    }

    public int ScopeIndex { get => scope; set { scope = value; Changed(); } }
    public bool Stretch { get => stretch; set { stretch = value; Changed(); } }
    public bool Autostart { get => autostart; set { autostart = value; Changed(); } }
    public bool NoGames => Rows.Count == 0;

    // the language applies and is stored right away, like in captureMax; everything else waits for Save
    public string Lang
    {
        get => L.I.Lang;
        set
        {
            L.I.Lang = value;
            Helper.Settings = Helper.Settings with { Lang = value };
            Helper.Settings.Save();
            Notify(nameof(StatusText), nameof(ActionText));
        }
    }

    public bool Dirty => Snapshot(Current(), autostart) != saved;

    public string StatusText => Helper.Active is Game g ? L.I.Format("active", g.Exe, g.Width, g.Height) : L.I["ready"];
    public Brush StatusBrush => (Brush)FindResource(Helper.Active is null ? "Muted" : "Green");

    string? progress;
    public bool ShowAction { get; private set { field = value; Changed(); } }
    public bool ActionEnabled => progress is null;
    public string ActionText => progress ?? (update is { } u ? L.I.Format("update", u.Version) : L.I["install"]);

    void Load()
    {
        var s = Helper.Settings;
        Rows.Clear();
        foreach (var g in s.Games) AddRow(g);
        scope = (int)s.Taskbar;
        stretch = s.Stretch;
        autostart = Installer.Autostart;
        saved = Snapshot(s, autostart);
        Notify(""); // refresh every binding
    }

    void AddRow(Game g)
    {
        var row = new GameRow(g);
        row.PropertyChanged += (_, _) => Notify(nameof(Dirty));
        Rows.Add(row);
        Notify(nameof(Dirty));
    }

    Settings Current() => new(Rows.Select(r => r.ToGame()).ToList(), (TaskbarScope)scope, stretch, L.I.Lang);
    static string Snapshot(Settings s, bool autostart) => (s with { Lang = null }).ToJson() + autostart;

    void Save_Click(object sender, RoutedEventArgs e)
    {
        var biggest = System.Windows.Forms.Screen.AllScreens.Select(s => s.Bounds).MaxBy(b => b.Width * b.Height);
        var s = Current();
        s = s with { Games = s.Games.Select(g => g with { Width = Math.Clamp(g.Width, 320, biggest.Width), Height = Math.Clamp(g.Height, 200, biggest.Height) }).ToList() };
        s.Save();
        Helper.Settings = s;
        Helper.ShowTaskbar(); // the next tick hides it again with the new settings
        if (autostart != Installer.Autostart) Installer.SetAutostart(autostart);
        Load();
    }

    void Add_Click(object sender, RoutedEventArgs e)
    {
        var picker = new AppPicker { Owner = this };
        if (picker.ShowDialog() != true || picker.Picked is not AppEntry app) return;
        if (Rows.Any(r => r.Exe.Equals(app.Exe, StringComparison.OrdinalIgnoreCase))) return;
        var screen = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        var (w, h) = Displays.DefaultSize(screen.Width, screen.Height);
        AddRow(new Game(app.Name, app.Exe, w, h, true, app.Path));
    }

    void Remove_Click(object sender, RoutedEventArgs e) => Rows.Remove((GameRow)((Button)sender).DataContext);

    // ---- install / update (captureMax: Main.install, show_update, self_update)
    internal void ShowUpdate(Installer.Update u)
    {
        update = u;
        ShowAction = true;
        Notify(nameof(ActionText));
        if (IsVisible && Dialog.Ask(this, L.I.Format(u.Url is null ? "update_ask" : "update_self", "v" + u.Version, VersionText))) _ = RunUpdate();
    }

    void Action_Click(object sender, RoutedEventArgs e)
    {
        if (update is not null) _ = RunUpdate();
        else Install();
    }

    void Install()
    {
        if (!Dialog.Ask(this, L.I["install_ask"])) return;
        try { Installer.Install(); }
        catch (Exception e) { Dialog.Tell(this, L.I.Format("install_fail", e.Message)); return; } // e.g. the installed copy is running
        Program.Restart(Installer.Exe);
    }

    async Task RunUpdate()
    {
        var releases = $"https://github.com/{Installer.Repo}/releases/latest";
        if (update!.Url is null) { Installer.Open(releases); return; }
        try
        {
            SetProgress(L.I.Format("downloading", 0));
            await Installer.Apply(update, new Progress<int>(p => SetProgress(L.I.Format("downloading", p))));
        }
        catch (Exception e)
        {
            SetProgress(null);
            Dialog.Tell(this, L.I.Format("update_fail", e.Message));
            update = update with { Url = null }; // the button now opens the release page instead
            Installer.Open(releases);
            return;
        }
        Program.Restart(Installer.Exe);
    }

    void SetProgress(string? text)
    {
        progress = text;
        Notify(nameof(ActionText), nameof(ActionEnabled));
    }

    // closing only hides to the tray (the helper keeps running); unsaved changes need a confirm, then get reverted
    protected override void OnClosing(CancelEventArgs e)
    {
        if (Quitting) return;
        e.Cancel = true;
        if (Dirty)
        {
            if (!Dialog.Ask(this, L.I["discard"])) return;
            Load();
        }
        Hide();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void Changed([CallerMemberName] string name = "") => Notify(name);
    void Notify(params string[] names)
    {
        foreach (var n in names) PropertyChanged?.Invoke(this, new(n));
        if (!names.Contains(nameof(Dirty))) PropertyChanged?.Invoke(this, new(nameof(Dirty)));
    }
}

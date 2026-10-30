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
    public Game Game { get; private set; } = game;
    public string Name => Game.Name;
    public string Exe => Game.Exe;
    public ImageSource? Icon => Game.Path is null ? null : Apps.Icon(Game.Path);
    public bool Enabled { get => Game.Enabled; set => Update(Game with { Enabled = value }); }

    // "2560x1440 · default", "1920x1080 · 16:9" or "above the taskbar" (keep frame: the game's own size)
    public string Size => Game.KeepFrame ? L.I["above_taskbar"] : $"{Helper.Settings.SizeOf(Game).W}x{Helper.Settings.SizeOf(Game).H}";
    public string SizeInfo => Game.KeepFrame ? "" : Game.Width is null ? L.I["default_tag"]
        : Displays.Aspect(Helper.Settings.SizeOf(Game).W, Helper.Settings.SizeOf(Game).H);

    public void Update(Game g)
    {
        Game = g;
        Refresh();
        Edited?.Invoke();
    }

    public void Refresh() => PropertyChanged?.Invoke(this, new("")); // "" = every property

    public event Action? Edited;
    public event PropertyChangedEventHandler? PropertyChanged;
}

// Every change is saved right away: the games, the default size, the taskbar options, the language.
public partial class MainWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<GameRow> Rows { get; } = [];
    public ResolutionChoice Default { get; }
    public List<LangItem> LangItems { get; } =
        L.Languages.Select(l => new LangItem(l.Key, l.Value, (ImageSource)Application.Current.FindResource("Flag_" + l.Key))).ToList();
    public string VersionText { get; } = Installer.Version == "dev" ? "dev" : "v" + Installer.Version;

    int scope, mode;
    bool stretch;
    bool loading = true;
    Installer.Update? update;
    public bool Quitting;

    public MainWindow()
    {
        InitializeComponent();
        Icon = Dialog.Icon;
        DataContext = this;
        SourceInitialized += (_, _) => Dialog.DarkTitleBar(this);
        Rows.CollectionChanged += (_, _) => Changed(nameof(NoGames));

        var s = Helper.Settings;
        foreach (var g in s.Games) AddRow(g);
        var (w, h) = s.DefaultSize;
        Default = new ResolutionChoice(w, h);
        Default.PropertyChanged += (_, _) => Persist();
        scope = (int)s.Taskbar;
        mode = (int)s.Mode;
        stretch = s.Stretch;
        ShowAction = !Installer.Installed; // portable: offer the install
        loading = false;
    }

    public int ScopeIndex { get => scope; set { scope = value; Persist(); } }
    public int ModeIndex { get => mode; set { mode = value; Persist(); Changed(nameof(Hiding)); } }
    public bool Hiding => mode == (int)TaskbarMode.Hide;
    public bool Stretch { get => stretch; set { stretch = value; Persist(); } }
    public bool Autostart { get => Installer.Autostart; set => Installer.SetAutostart(value); }
    public bool NoGames => Rows.Count == 0;

    public string Lang
    {
        get => L.I.Lang;
        set
        {
            L.I.Lang = value;
            Persist();
            Changed(nameof(ActionText));
        }
    }

    void Persist()
    {
        if (loading) return;
        Helper.Settings = new Settings(Rows.Select(r => r.Game).ToList(), (TaskbarScope)scope, stretch, L.I.Lang, Default.Width, Default.Height,
            (TaskbarMode)mode);
        Helper.Settings.Save();
        Helper.ShowTaskbar(); // the next tick hides it again with the new settings
        foreach (var r in Rows) r.Refresh(); // sizes that follow the default
    }

    void AddRow(Game g)
    {
        var row = new GameRow(g);
        row.Edited += Persist;
        Rows.Add(row);
    }

    void Add_Click(object sender, RoutedEventArgs e)
    {
        var picker = new AppPicker { Owner = this };
        if (picker.ShowDialog() != true || picker.Picked is not AppEntry app) return;
        if (Rows.Any(r => r.Exe.Equals(app.Exe, StringComparison.OrdinalIgnoreCase) && r.Game.Title == app.Title)) return;
        AddRow(new Game(app.Name, app.Exe, Path: app.Path == "" ? null : app.Path, Title: app.Title)); // follows the default size until edited
        Persist();
    }

    void Edit_Click(object sender, RoutedEventArgs e)
    {
        var row = (GameRow)((Button)sender).DataContext;
        var editor = new GameEditor(row.Game, Helper.Settings.DefaultSize) { Owner = this };
        if (editor.ShowDialog() == true) row.Update(editor.Result!);
    }

    void Remove_Click(object sender, RoutedEventArgs e)
    {
        Rows.Remove((GameRow)((Button)sender).DataContext);
        Persist();
    }

    // ---- install / update (captureMax: Main.install, show_update, self_update)
    string? progress;
    public bool ShowAction { get; private set { field = value; Changed(); } }
    public bool ActionEnabled => progress is null;
    public string ActionText => progress ?? (update is { } u ? L.I.Format("update", u.Version) : L.I["install"]);

    internal void ShowUpdate(Installer.Update u)
    {
        update = u;
        ShowAction = true;
        Changed(nameof(ActionText));
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
        Changed(nameof(ActionText));
        Changed(nameof(ActionEnabled));
    }

    // closing only hides to the tray, the helper keeps running
    protected override void OnClosing(CancelEventArgs e)
    {
        if (Quitting) return;
        e.Cancel = true;
        Hide();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void Changed([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new(name));
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace BorderlessMax;

public record LangItem(string Code, string Name, ImageSource Flag);

// A path that doesn't fit is cut at the start ("…\Game\League of Legends.exe"): the end is what tells files apart.
public class PathBlock : Decorator
{
    public static readonly DependencyProperty PathProperty = DependencyProperty.Register(nameof(Path), typeof(string), typeof(PathBlock),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure));
    public string Path { get => (string)GetValue(PathProperty); set => SetValue(PathProperty, value); }
    readonly TextBlock text = new();

    public PathBlock() => Child = text;

    protected override Size MeasureOverride(Size available)
    {
        var s = Path;
        for (int i = 1; i < Path.Length && WidthOf(s) > available.Width; i++) s = "…" + Path[i..];
        text.Text = s;
        return base.MeasureOverride(available);
    }

    double WidthOf(string s) => new FormattedText(s, System.Globalization.CultureInfo.CurrentCulture, FlowDirection,
        new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize, Brushes.White,
        VisualTreeHelper.GetDpi(this).PixelsPerDip).WidthIncludingTrailingWhitespace;
}

// One game in the list and its settings on the right; every valid change is saved right away, an invalid
// name or exe only marks its field red (the setter throws, the binding shows it).
public class GameRow : INotifyPropertyChanged
{
    public GameRow(Game game)
    {
        Game = game;
        var (w, h) = Helper.Settings.SizeOf(game);
        Choice = new ResolutionChoice(w, h);
        Choice.PropertyChanged += (_, _) => { if (!UseDefault) Update(Game with { Width = Choice.Width, Height = Choice.Height }); };
    }

    public Game Game { get; private set; }
    public ImageSource? Icon => (Game.Icon ?? Game.Path) is string file ? Apps.Icon(file) : null;
    public string PathText => Game.Path ?? L.I["path_unknown"];
    public string IconText => Game.Icon ?? Game.Path ?? L.I["path_unknown"];
    public bool HasPath => Game.Path is not null;
    public bool HasIcon => (Game.Icon ?? Game.Path) is not null;
    public bool Enabled { get => Game.Enabled; set => Update(Game with { Enabled = value }); }

    public string Name
    {
        get => Game.Name;
        set => Update(Game with { Name = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("empty") : value });
    }

    public string Exe
    {
        get => Game.Exe;
        set => Update(Game with { Exe = value.Trim().EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? value.Trim() : throw new ArgumentException("no exe") });
    }

    // own size: starts at the current default, then follows the picker
    public ResolutionChoice Choice { get; }
    public bool UseDefault
    {
        get => Game.Width is null;
        set
        {
            if (!value) (Choice.Width, Choice.Height) = Helper.Settings.DefaultSize;
            Update(value ? Game with { Width = null, Height = null } : Game with { Width = Choice.Width, Height = Choice.Height });
        }
    }
    public bool OwnSize => !UseDefault;
    public string DefaultText => L.I.Format("use_default", Helper.Settings.DefaultSize.W, Helper.Settings.DefaultSize.H);

    // "2560x1440 · default" or "1920x1080 · 16:9"
    public string SizeText => $"{Helper.Settings.SizeOf(Game).W}x{Helper.Settings.SizeOf(Game).H}";
    public string SizeInfo => Game.Width is null ? L.I["default_tag"] : Displays.Aspect(Helper.Settings.SizeOf(Game).W, Helper.Settings.SizeOf(Game).H);

    public void Update(Game g)
    {
        Game = g;
        Refresh();
        Edited?.Invoke();
    }

    // changed outside the window (Helper learned its file): already saved
    public void Replace(Game g)
    {
        Game = g;
        Refresh();
    }

    public void Refresh() =>PropertyChanged?.Invoke(this, new("")); // "" = every property

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

    int scope, mode, sort;
    bool stretch;
    readonly ICollectionView view; // the list as shown: searched and sorted, Rows keeps the order they were added in
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
        Helper.GameLearned += (old, learned) => Rows.FirstOrDefault(r => r.Game == old)?.Replace(learned);

        var s = Helper.Settings;
        foreach (var g in s.Games) AddRow(g);
        var (w, h) = s.DefaultSize;
        Default = new ResolutionChoice(w, h);
        Default.PropertyChanged += (_, _) => Persist();
        scope = (int)s.Taskbar;
        mode = (int)s.Mode;
        stretch = s.Stretch;
        view = CollectionViewSource.GetDefaultView(Rows);
        view.Filter = o => Matches((GameRow)o, search.Text);
        SortIndex = s.Sort;
        Selected = view.Cast<GameRow>().FirstOrDefault(); // the top one as sorted
        ShowAction = !Installer.Installed; // portable: offer the install
        loading = false;
    }

    public int ScopeIndex { get => scope; set { scope = value; Persist(); } }
    public int ModeIndex { get => mode; set { mode = value; Persist(); Changed(nameof(Hiding)); } }
    public bool Hiding => mode == (int)TaskbarMode.Hide;
    public bool Stretch { get => stretch; set { stretch = value; Persist(); } }

    // ponytail: sorted / filtered when the sort or the search changes, not while a game is edited (a renamed game keeps its place)
    public int SortIndex
    {
        get => sort;
        set
        {
            sort = value;
            using (view.DeferRefresh())
            {
                view.SortDescriptions.Clear();
                if (sort == 2) view.SortDescriptions.Add(new(nameof(GameRow.Enabled), ListSortDirection.Descending));
                if (sort > 0) view.SortDescriptions.Add(new(nameof(GameRow.Name), ListSortDirection.Ascending));
            }
            Persist();
        }
    }

    // every word must appear in the name or the exe, like the picker
    static bool Matches(GameRow r, string query) =>
        query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w => $"{r.Name} {r.Exe}".Contains(w, StringComparison.OrdinalIgnoreCase));

    void Search_Changed(object sender, TextChangedEventArgs e)
    {
        placeholder.Visibility = search.Text == "" ? Visibility.Visible : Visibility.Collapsed;
        view.Refresh();
    }
    public bool Autostart { get => Installer.Autostart; set => Installer.SetAutostart(value); }
    public bool NoGames => Rows.Count == 0;
    public GameRow? Selected { get; set { field = value; Changed(); Changed(nameof(HasSelection)); } }
    public bool HasSelection => Selected is not null;
    public bool ShowDetails { get; set { field = value; Changed(); } }

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
            (TaskbarMode)mode, sort);
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
        // already in the list: just show it
        Selected = Rows.FirstOrDefault(r => r.Exe.Equals(app.Exe, StringComparison.OrdinalIgnoreCase) && r.Game.Title == app.Title);
        if (Selected is not null)
        {
            if (Selected.Game.Path is null && app.Path != "") Selected.Update(Selected.Game with { Path = app.Path }); // its file was unknown
            return;
        }
        AddRow(new Game(app.Name, app.Exe, Path: app.Path == "" ? null : app.Path, Title: app.Title, Icon: app.IconPath)); // follows the default size until edited
        Selected = Rows[^1];
        Persist();
    }

    // any image or exe; the game's Path stays as it is
    void Icon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = $"{L.I["icons"]}|{string.Join(";", Apps.ImageTypes.Select(t => "*" + t))};*.exe" };
        if (Selected is null || dialog.ShowDialog(this) != true) return;
        Selected.Update(Selected.Game with { Icon = dialog.FileName });
    }

    void Details_Click(object sender, RoutedEventArgs e) => ShowDetails = !ShowDetails;
    void Copy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText((string)((Button)sender).Tag); }
        catch (System.Runtime.InteropServices.COMException) { } // clipboard held by another app: the click just does nothing
    }

    void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is null) return;
        var i = Rows.IndexOf(Selected);
        Rows.Remove(Selected);
        Selected = Rows.Count == 0 ? null : Rows[Math.Min(i, Rows.Count - 1)]; // the next one, or the last
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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace BorderlessMax;

// Settings of one game: name, exe, and the default size or its own.
public partial class GameEditor : Window, INotifyPropertyChanged
{
    readonly Game game;
    readonly (int W, int H) defaultSize;
    public Game? Result { get; private set; }

    public GameEditor(Game game, (int W, int H) defaultSize)
    {
        InitializeComponent();
        this.game = game;
        this.defaultSize = defaultSize;
        gameName = game.Name;
        exe = game.Exe;
        useDefault = game.Width is null;
        keepFrame = game.KeepFrame;
        Size = new ResolutionChoice(game.Width ?? defaultSize.W, game.Height ?? defaultSize.H);
        Icon = Dialog.Icon;
        DataContext = this;
        SourceInitialized += (_, _) => Dialog.DarkTitleBar(this);
    }

    public ImageSource? GameIcon => game.Path is null ? null : Apps.Icon(game.Path);
    public ResolutionChoice Size { get; }
    public string DefaultText => L.I.Format("use_default", defaultSize.W, defaultSize.H);

    string gameName, exe;
    bool useDefault;
    public string GameName { get => gameName; set { gameName = value; Changed(); Changed(nameof(Valid)); } }
    public string Exe { get => exe; set { exe = value; Changed(); Changed(nameof(Valid)); } }
    public bool UseDefault { get => useDefault; set { useDefault = value; Changed(); Changed(nameof(OwnSize)); } }
    public bool OwnSize => !useDefault;
    bool keepFrame;
    public bool KeepFrame { get => keepFrame; set { keepFrame = value; Changed(); Changed(nameof(Borderless)); } }
    public bool Borderless => !keepFrame;
    public bool Valid => gameName.Trim() != "" && exe.Trim().EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    void Save_Click(object sender, RoutedEventArgs e)
    {
        Result = game with
        {
            Name = gameName.Trim(), Exe = exe.Trim(),
            Width = useDefault ? null : Size.Width, Height = useDefault ? null : Size.Height, KeepFrame = keepFrame,
        };
        DialogResult = true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void Changed([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new(name));
}

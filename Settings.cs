using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BorderlessMax;

// Exe is matched against the process name; Path only gives the list its icon; no Width / Height = the default size
public record Game(string Name, string Exe, int? Width = null, int? Height = null, bool Enabled = true, string? Path = null);

public enum TaskbarScope { GameScreen, AllScreens }

// Behind: the taskbar stays, the focused game is drawn in front of it; Hide: the taskbar is hidden (on Taskbar's screens)
public enum TaskbarMode { Behind, Hide }

// Stretch: taskbar auto-hide while playing, so maximized windows reach the bottom of the screen.
// DefaultWidth / DefaultHeight 0 = 16:9 at the height of the main screen.
public record Settings(List<Game> Games, TaskbarScope Taskbar = TaskbarScope.GameScreen, bool Stretch = true, string? Lang = null,
    int DefaultWidth = 0, int DefaultHeight = 0, TaskbarMode Mode = TaskbarMode.Behind)
{
    // %APPDATA%, not next to the exe: survives updates and is the same for the portable and the installed copy
    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), App.Name);
    static readonly string FilePath = Path.Combine(Dir, "settings.json");
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true, Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [JsonIgnore]
    public (int W, int H) DefaultSize
    {
        get
        {
            if (DefaultWidth > 0 && DefaultHeight > 0) return (DefaultWidth, DefaultHeight);
            var screen = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
            return Displays.DefaultSize(screen.Width, screen.Height);
        }
    }

    // a game's own size, else the default; never smaller than a usable window (the screen size caps it in Helper.Center)
    public (int W, int H) SizeOf(Game g)
    {
        var (w, h) = g.Width is int gw && g.Height is int gh ? (gw, gh) : DefaultSize;
        return (Math.Max(w, 320), Math.Max(h, 200));
    }

    public static Settings Load()
    {
        try { return Parse(File.ReadAllText(FilePath)); }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException) { return new([]); }
        catch (JsonException)
        {
            File.Copy(FilePath, FilePath + ".broken", true); // keep the user's games, a save would overwrite them
            return new([]);
        }
    }

    public static Settings Parse(string json) => JsonSerializer.Deserialize<Settings>(json, Json) ?? new([]);
    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, ToJson());
    }
}

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BorderlessMax;

// Exe is matched against the process name; Path only gives the list its icon
public record Game(string Name, string Exe, int Width, int Height, bool Enabled = true, string? Path = null);

public enum TaskbarScope { GameScreen, AllScreens }

// Stretch: taskbar auto-hide while playing, so maximized windows reach the bottom of the screen
public record Settings(List<Game> Games, TaskbarScope Taskbar = TaskbarScope.GameScreen, bool Stretch = true, string? Lang = null)
{
    // %APPDATA%, not next to the exe: survives updates and is the same for the portable and the installed copy
    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), App.Name);
    static readonly string FilePath = Path.Combine(Dir, "settings.json");
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true, Converters = { new JsonStringEnumConverter() },
    };

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

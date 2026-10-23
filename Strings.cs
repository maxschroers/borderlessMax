using System.ComponentModel;
using System.Globalization;

namespace BorderlessMax;

// UI text in German and English. XAML binds to L.I[key]; switching the language refreshes every binding.
public class L : INotifyPropertyChanged
{
    public static readonly L I = new();
    public static readonly Dictionary<string, string> Languages = new() { ["de"] = "Deutsch", ["en"] = "English" };
    public static string Default => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? "de" : "en";

    static readonly Dictionary<string, Dictionary<string, string>> TR = new()
    {
        ["de"] = new()
        {
            ["subtitle"] = "Randlos-Fenster-Helfer", ["games"] = "Spiele & Apps", ["add_game"] = "Spiel hinzufügen",
            ["no_games"] = "Noch keine Spiele. Füge eins mit dem Button unten hinzu.",
            ["taskbar"] = "Taskleiste", ["hide_on"] = "Taskleiste ausblenden auf",
            ["taskbar_mode"] = "Beim Spielen", ["mode_behind"] = "Hinter dem Spiel", ["mode_hide"] = "Ausblenden",
            ["mode_hint"] = "Hinter dem Spiel: Die Taskleiste bleibt sichtbar, das fokussierte Spiel liegt davor. Ausblenden: Sie verschwindet, bis du rausgehst.", ["game_screen"] = "Spiel-Bildschirm",
            ["all_screens"] = "Allen Bildschirmen", ["stretch"] = "Fenster bis unten strecken",
            ["stretch_hint"] = "Schaltet beim Spielen das automatische Ausblenden der Taskleiste ein, damit maximierte Fenster bis zum Bildschirmrand reichen.",
            ["general"] = "Allgemein", ["autostart"] = "Mit Windows starten", ["autostart_hint"] = "Startet unsichtbar im Infobereich.",
            ["save"] = "Speichern", ["cancel"] = "Abbrechen",
            ["custom"] = "Eigene…", ["screen"] = "Bildschirm {0}", ["enabled"] = "Aktiv", ["remove"] = "Entfernen", ["edit"] = "Bearbeiten",
            ["default_res"] = "Standard-Auflösung", ["default_hint"] = "Gilt für alle Spiele ohne eigene Auflösung. Eigene Auflösung: ✎ in der Zeile des Spiels.",
            ["default_tag"] = "Standard", ["edit_title"] = "Spiel bearbeiten", ["name"] = "Name", ["exe"] = "Programm",
            ["exe_hint"] = "Der Prozessname, an dem das Spiel erkannt wird, z. B. eldenring.exe.",
            ["resolution"] = "Auflösung", ["use_default"] = "Standard-Auflösung verwenden ({0}x{1})",
            ["window"] = "Fenster", ["keep_frame"] = "Rahmen behalten, über die Taskleiste setzen", ["above_taskbar"] = "über der Taskleiste",
            ["keep_frame_hint"] = "Für Spiele, die sich nicht randlos machen lassen (z. B. VALORANT im Modus Fenster): Größe und Titelleiste bleiben, das Fenster wird mittig direkt über die Taskleiste gesetzt.",
            ["install"] = "Installieren",
            ["install_ask"] = "borderlessMax für dich installieren?\nKommt ins Startmenü, aktualisiert sich selbst und lässt sich unter Einstellungen → Apps wieder entfernen.",
            ["install_fail"] = "Installation fehlgeschlagen: {0}", ["uninstall_ask"] = "borderlessMax deinstallieren?",
            ["update"] = "Update auf v{0}",
            ["update_ask"] = "Version {0} ist verfügbar (installiert: {1}).\nJetzt herunterladen?",
            ["update_self"] = "Version {0} ist verfügbar (installiert: {1}).\nJetzt aktualisieren? borderlessMax startet danach neu.",
            ["downloading"] = "Lade Update... {0} %", ["update_fail"] = "Update fehlgeschlagen: {0}\nDie Download-Seite wird geöffnet.",
            ["yes"] = "Ja", ["no"] = "Nein", ["ok"] = "OK",
            ["picker"] = "Spiel oder App wählen", ["search"] = "Programme und Spiele durchsuchen", ["browse"] = "Durchsuchen…",
            ["scanning"] = "Suche Programme und Spiele auf diesem PC…", ["nothing"] = "Nichts gefunden. Probier „Durchsuchen…“.",
            ["programs"] = "Programme", ["running"] = "Läuft", ["start_menu"] = "Startmenü", ["installed"] = "Installiert",
            ["open"] = "borderlessMax öffnen", ["exit"] = "Beenden", ["crashed"] = "borderlessMax ist abgestürzt",
        },
        ["en"] = new()
        {
            ["subtitle"] = "borderless window helper", ["games"] = "Games & apps", ["add_game"] = "Add game",
            ["no_games"] = "No games yet. Add one with the button below.",
            ["taskbar"] = "Taskbar", ["hide_on"] = "Hide taskbar on",
            ["taskbar_mode"] = "While playing", ["mode_behind"] = "Behind the game", ["mode_hide"] = "Hide",
            ["mode_hint"] = "Behind the game: the taskbar stays visible, the focused game is in front of it. Hide: it disappears until you tab out.", ["game_screen"] = "Game screen",
            ["all_screens"] = "All screens", ["stretch"] = "Stretch windows to the bottom",
            ["stretch_hint"] = "Turns on taskbar auto-hide while you play, so maximized windows reach the edge of the screen.",
            ["general"] = "General", ["autostart"] = "Start with Windows", ["autostart_hint"] = "Starts hidden in the tray.",
            ["save"] = "Save", ["cancel"] = "Cancel",
            ["custom"] = "Custom…", ["screen"] = "Screen {0}", ["enabled"] = "Enabled", ["remove"] = "Remove", ["edit"] = "Edit",
            ["default_res"] = "Default resolution", ["default_hint"] = "Used by every game without its own resolution. Own resolution: ✎ in the game's row.",
            ["default_tag"] = "default", ["edit_title"] = "Edit game", ["name"] = "Name", ["exe"] = "Program",
            ["exe_hint"] = "The process name the game is recognised by, e.g. eldenring.exe.",
            ["resolution"] = "Resolution", ["use_default"] = "Use default resolution ({0}x{1})",
            ["window"] = "Window", ["keep_frame"] = "Keep the frame, place above the taskbar", ["above_taskbar"] = "above the taskbar",
            ["keep_frame_hint"] = "For games that won't go borderless (e.g. VALORANT in windowed mode): size and title bar stay, the window is centered right above the taskbar.",
            ["install"] = "Install",
            ["install_ask"] = "Install borderlessMax for you?\nIt goes into the Start menu, keeps itself up to date and can be removed under Settings → Apps.",
            ["install_fail"] = "Installation failed: {0}", ["uninstall_ask"] = "Uninstall borderlessMax?",
            ["update"] = "Update to v{0}",
            ["update_ask"] = "Version {0} is available (installed: {1}).\nDownload it now?",
            ["update_self"] = "Version {0} is available (installed: {1}).\nUpdate now? borderlessMax restarts afterwards.",
            ["downloading"] = "Downloading update... {0} %", ["update_fail"] = "Update failed: {0}\nOpening the download page.",
            ["yes"] = "Yes", ["no"] = "No", ["ok"] = "OK",
            ["picker"] = "Pick a game or app", ["search"] = "Search programs and games", ["browse"] = "Browse…",
            ["scanning"] = "Searching programs and games on this PC…", ["nothing"] = "Nothing found. Try “Browse…”.",
            ["programs"] = "Programs", ["running"] = "Running", ["start_menu"] = "Start menu", ["installed"] = "Installed",
            ["open"] = "Open borderlessMax", ["exit"] = "Exit", ["crashed"] = "borderlessMax crashed",
        },
    };

    string lang = Default;
    public string Lang
    {
        get => lang;
        set
        {
            lang = TR.ContainsKey(value) ? value : "en";
            PropertyChanged?.Invoke(this, new(nameof(Lang)));
            PropertyChanged?.Invoke(this, new("Item[]")); // every {Binding [key]} reads again
        }
    }

    public string this[string key] => TR[lang][key];
    public string Format(string key, params object[] args) => string.Format(TR[lang][key], args);
    public static IEnumerable<string> Keys(string lang) => TR[lang].Keys;

    public event PropertyChangedEventHandler? PropertyChanged;
}

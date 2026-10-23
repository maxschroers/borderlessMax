using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace BorderlessMax;

// Picks a game or app: everything Apps.Scan finds on this PC, searchable, or any exe via the file dialog.
public partial class AppPicker : Window
{
    static List<AppEntry>? found; // scanning takes a few seconds: once per session
    ICollectionView? view;
    public AppEntry? Picked { get; private set; }

    public AppPicker()
    {
        InitializeComponent();
        Icon = Dialog.Icon;
        SourceInitialized += (_, _) => Dialog.DarkTitleBar(this);
        Loaded += async (_, _) =>
        {
            search.Focus();
            note.Text = L.I["scanning"];
            found ??= await Task.Run(Apps.Scan);
            view = CollectionViewSource.GetDefaultView(found);
            view.Filter = o => Matches((AppEntry)o, search.Text);
            list.ItemsSource = view;
            UpdateNote();
        };
    }

    // every word must appear in the name, the exe or the source: "elden ring", "shipping", "steam"
    public static bool Matches(AppEntry app, string query) =>
        query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(w => $"{app.Name} {app.Exe} {app.Source}".Contains(w, StringComparison.OrdinalIgnoreCase));

    void UpdateNote() => note.Text = view?.IsEmpty == true ? L.I["nothing"] : "";

    void Search_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        placeholder.Visibility = search.Text == "" ? Visibility.Visible : Visibility.Collapsed;
        view?.Refresh();
        UpdateNote();
    }

    // arrow down from the search box walks the list, Enter takes the first hit
    void Search_Keys(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && !list.Items.IsEmpty)
        {
            list.SelectedIndex = Math.Max(0, list.SelectedIndex);
            list.UpdateLayout();
            (list.ItemContainerGenerator.ContainerFromIndex(list.SelectedIndex) as UIElement)?.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !list.Items.IsEmpty)
        {
            list.SelectedIndex = Math.Max(0, list.SelectedIndex);
            Pick();
        }
    }

    void List_Keys(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Pick();
    }

    void List_DoubleClick(object sender, MouseButtonEventArgs e) => Pick();
    void List_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => add.IsEnabled = list.SelectedItem is not null;
    void Add_Click(object sender, RoutedEventArgs e) => Pick();

    void Pick()
    {
        if (list.SelectedItem is not AppEntry app) return;
        Picked = app;
        DialogResult = true;
    }

    void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = $"{L.I["programs"]} (*.exe)|*.exe" };
        if (dialog.ShowDialog(this) != true) return;
        Picked = new(Apps.Describe(dialog.FileName), dialog.FileName, "");
        DialogResult = true;
    }
}

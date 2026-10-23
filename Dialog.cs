using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace BorderlessMax;

static class Dialog
{
    public static readonly BitmapFrame Icon = BitmapFrame.Create(new Uri("pack://application:,,,/assets/icon.png"));

    // Windows draws a light title bar by default: ask DWM for a dark one in captureMax's title bar colour (#121214)
    public static void DarkTitleBar(Window w)
    {
        var hwnd = new WindowInteropHelper(w).EnsureHandle();
        int dark = 1, caption = 0x14 << 16 | 0x12 << 8 | 0x12; // COLORREF is 0x00BBGGRR
        Native.DwmSetWindowAttribute(hwnd, 20, ref dark, 4);
        Native.DwmSetWindowAttribute(hwnd, 35, ref caption, 4);
    }

    public static bool Ask(Window? owner, string text) => Show(owner, text, true);
    public static void Tell(Window? owner, string text) => Show(owner, text, false);

    // modal yes / no (or OK) in the app's style and language; Windows' own MessageBox is light and English-only
    static bool Show(Window? owner, string text, bool question)
    {
        var w = new Window
        {
            Title = App.Name, Icon = Icon, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = owner is null, Style = (Style)Application.Current.FindResource("Win"),
            WindowStartupLocation = owner is { IsVisible: true } ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
        };
        if (owner is { IsVisible: true }) w.Owner = owner;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 20, 0, 0) };
        Button Add(string key, string style, bool result)
        {
            var b = new Button { Content = L.I[key], Style = (Style)Application.Current.FindResource(style), MinHeight = 32, MinWidth = 90, Margin = new(8, 0, 0, 0) };
            b.Click += (_, _) => w.DialogResult = result;
            buttons.Children.Add(b);
            return b;
        }
        if (question)
        {
            Add("yes", "Primary", true); // blurple like Discord's
            Add("no", "GreyButton", false).IsCancel = true;
        }
        else Add("ok", "Primary", true).IsCancel = true;
        w.Content = new StackPanel
        {
            Margin = new(24, 20, 24, 20),
            Children = { new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 420, LineHeight = 20 }, buttons },
        };
        w.SourceInitialized += (_, _) => DarkTitleBar(w);
        return w.ShowDialog() == true;
    }
}

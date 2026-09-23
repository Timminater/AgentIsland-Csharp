using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AgentIsland.Core;
using AgentIsland.UI.Localization;
using AgentIsland.UI.Charts;
using AgentIsland.UI.Theme;

namespace AgentIsland.UI;

/// The release-notes card — the Windows port of the macOS 2.1.2 What's New
/// spread: poster hero, one page per theme, page dots, and a Get-started
/// close. Fires once per version.
public static class WhatsNewGate
{
    private const string SeenKey = "AgentIsland.whatsNewSeenVersion";

    public static string CurrentVersion =>
        typeof(WhatsNewGate).Assembly.GetName().Version?.ToString(3) ?? "0";

    /// Always fires once per version — deliberately NOT user-configurable
    /// (owner call, 1.7.2: every user walks through the release card once).
    public static void MaybeShow()
    {
        if (AppEnvironment.Current != AppMode.Normal) return;
        if (string.Equals(Preferences.Get<string?>(SeenKey), CurrentVersion, StringComparison.Ordinal)) return;
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.2),
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            WhatsNewWindow.Open();
        };
        timer.Start();
    }

    public static void MarkSeen() => Preferences.Set(SeenKey, CurrentVersion);
}

public sealed partial class WhatsNewWindow : Window
{
    private sealed record Page(
        string? ImageName, string Title, string Body,
        bool IsClosing = false, bool BrandHero = false);

    /// Current release pages. Keep copy capability-based instead of tying it
    /// to an obsolete provider count or another operating system.
    private static Page[] Pages => new[]
    {
        new Page(null, "At a glance",
            "Six providers share one island, with one active provider in the top bar and every enabled provider available from the switcher", BrandHero: true),
        new Page(null, "Approvals on the island",
            "Answer Claude Code permissions, questions and plan reviews without losing the context of the active session", BrandHero: true),
        new Page(null, "Know when usage runs out",
            "Below your chosen remaining-quota threshold, the top bar can estimate time to empty from recent usage — and automatically go quiet during pauses", BrandHero: true),
        new Page("whatsnew-terminal", "Back to your terminal",
            "An alarm click lands in the session's live window — Windows Terminal, a console, or an IDE pane. A fresh terminal opens only when nothing is running"),
        new Page("whatsnew-tones", "Windows alarm tones",
            "Chimes, Xylophone, Chords — the alarm rings with Windows' own tones, played straight from the system's alarm library. Chimes is the new default"),
        new Page(null, "Cost across all providers",
            "Local usage records feed costs and reports for Claude, Codex, Antigravity, Grok, Cursor and DeepSeek", BrandHero: true),
        new Page("whatsnew-start", "Get started",
            "Welcome back to Agent Island", IsClosing: true),
    };

    /// The global product tour — the whole current Windows product.
    private static Page[] GuidePages => new[]
    {
        new Page(null, "Live status and quota, together",
            "Six providers on one island — each read from the local records it already writes on this computer",
            BrandHero: true),
        new Page(null, "Monitor",
            "Enabled providers carry live session state where their local tools expose it. Spinning means working, a bell means it's your turn, and steady red means it needs you", BrandHero: true),
        new Page(null, "Usage",
            "Enable any number of providers. One is active in the top bar; use the switcher to move between the others", BrandHero: true),
        new Page(null, "Cost & history",
            "Local session logs become token counts, API value, and the year heatmap — nothing leaves your machine", BrandHero: true),
        new Page(null, "Report cards",
            "One click renders a shareable battle card — copy it or send it to your phone, and the arrows flip back to any past week or month", BrandHero: true),
        new Page(null, "Personalization",
            "Visual modes, glow colors, chart styles, language — and how alarms behave while you're in the session's app — all in Settings",
            IsClosing: true, BrandHero: true),
    };

    private static WhatsNewWindow? _open;

    private readonly Page[] _pages;
    private int _page;

    public static void Open()
    {
        if (_open is { } existing)
        {
            WindowActivation.BringToFront(existing);
            return;
        }
        _open = new WhatsNewWindow(Pages, showChip: true);
        _open.Closed += (_, _) =>
        {
            _open = null;
            WhatsNewGate.MarkSeen();
        };
        _open.Show();
        WindowActivation.BringToFront(_open);
    }

    /// The guide reuses the card wholesale; closing it never marks the
    /// release notes as seen.
    public static void OpenGuide()
    {
        if (_open is { } existing)
        {
            WindowActivation.BringToFront(existing);
            return;
        }
        _open = new WhatsNewWindow(GuidePages, showChip: false);
        _open.Closed += (_, _) => _open = null;
        _open.Show();
        WindowActivation.BringToFront(_open);
    }

    private WhatsNewWindow(Page[] pages, bool showChip)
    {
        InitializeComponent();
        _pages = pages;

        VersionText.Text = "v" + WhatsNewGate.CurrentVersion;
        VersionChip.Visibility = showChip ? Visibility.Visible : Visibility.Collapsed;
        BackText.Text = L10n.Tr("Back");

        PosterFrame.Loaded += (_, _) =>
        {
            PosterFrame.Clip = new RectangleGeometry(
                new Rect(0, 0, PosterFrame.ActualWidth, PosterFrame.ActualHeight), 14, 14);
        };

        MouseLeftButtonDown += (_, args) =>
        {
            if (args.ButtonState == MouseButtonState.Pressed) DragMove();
        };
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape) Close();
        };

        Flip(0);
    }

    private void Flip(int target)
    {
        _page = Math.Clamp(target, 0, _pages.Length - 1);
        var page = _pages[_page];

        PageTitle.Text = L10n.Tr(page.Title);
        PageBody.Text = L10n.Tr(page.Body);

        PosterContent.Children.Clear();
        if (page.BrandHero)
        {
            PosterContent.Children.Add(BuildBrandHero());
        }
        else if (page.ImageName is { } name && LoadPoster(name) is { } posterBmp)
        {
            var image = new Image
            {
                Source = posterBmp,
                Stretch = Stretch.UniformToFill,
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            PosterContent.Children.Add(image);
        }

        DotsPanel.Children.Clear();
        for (var i = 0; i < _pages.Length; i++)
        {
            var active = i == _page;
            var index = i;
            var dot = new Border
            {
                Width = active ? 16 : 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = active ? Brushes.White : IslandColors.Brush(IslandColors.White(0.16)),
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = Cursors.Hand,
            };
            dot.MouseLeftButtonUp += (_, args) =>
            {
                args.Handled = true;
                Flip(index);
            };
            DotsPanel.Children.Add(dot);
        }

        BackButton.Visibility = _page > 0 ? Visibility.Visible : Visibility.Collapsed;
        NextText.Text = _pages[_page].IsClosing
            ? L10n.Tr("Get started")
            : L10n.Tr("Next");
    }

    private void OnBackClicked(object sender, RoutedEventArgs e)
    {
        Flip(_page - 1);
    }

    private void OnNextClicked(object sender, RoutedEventArgs e)
    {
        if (_page >= _pages.Length - 1) Close();
        else Flip(_page + 1);
    }

    /// Use the language-neutral capture when one exists and fall back to the
    /// original artwork so a missing image never blanks the slot.
    private static BitmapImage? LoadPoster(string imageName)
    {
        try
        {
            return new BitmapImage(new Uri(
                $"pack://application:,,,/Assets/{imageName}-en.png"));
        }
        catch
        {
        }
        try
        {
            return new BitmapImage(new Uri($"pack://application:,,,/Assets/{imageName}.png"));
        }
        catch
        {
            return null;
        }
    }

    /// The guide's opening spread: the atmosphere poster behind the mark
    /// and wordmark, darkened just enough that the brand owns the frame.
    private static UIElement BuildBrandHero()
    {
        var frame = new Grid { Height = 226 };
        if (LoadPoster("guide-brand") is { } backdropBmp)
        {
            var backdrop = new Image
            {
                Source = backdropBmp,
                Stretch = Stretch.UniformToFill,
            };
            RenderOptions.SetBitmapScalingMode(backdrop, BitmapScalingMode.HighQuality);
            frame.Children.Add(backdrop);
        }
        frame.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(Color.FromArgb(0x59, 0, 0, 0)),
        });
        var overlay = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        try
        {
            var mark = new Image
            {
                Source = new BitmapImage(new Uri(
                    "pack://application:,,,/Assets/agentisland_logo.png")),
                Width = 52,
                Height = 52,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
            };
            RenderOptions.SetBitmapScalingMode(mark, BitmapScalingMode.HighQuality);
            overlay.Children.Add(mark);
        }
        catch
        {
        }
        overlay.Children.Add(new TextBlock
        {
            Text = "Agent Island",
            FontFamily = IslandFonts.Ui,
            FontSize = 23,
            FontWeight = FontWeights.Black,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        frame.Children.Add(overlay);
        return frame;
    }
}

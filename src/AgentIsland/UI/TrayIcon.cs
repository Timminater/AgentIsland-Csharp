using System.ComponentModel;
using AgentIsland.Core;
using AgentIsland.UI.Providers;
using AgentIsland.Core.Usage;

namespace AgentIsland.UI;

/// System tray entry point — the Windows-native home for the island's ambient
/// signal. The icon itself visualizes status (a usage ring that turns amber /
/// red for approaching-limit and attention states), a left click pops the
/// island open, and the menu holds show/hide, settings, and quit.
public sealed class TrayIcon : IDisposable
{
    /// Set by App at startup; the reminder center routes its system
    /// notification (balloon) through here.
    public static TrayIcon? Current { get; set; }

    private readonly System.Windows.Forms.NotifyIcon _icon;
    private readonly System.Windows.Threading.Dispatcher _dispatcher;
    private readonly ModernTrayMenu _modernMenu;
    private System.Drawing.Icon? _rendered;
    private TrayIconRenderer.VisualStateKey? _lastVisualKey;

    private readonly IUsageStore _usageStore;
    private readonly Backend.Monitoring.IActivityMonitor _activityMonitor;
    private readonly Backend.Settings.IProviderVisibilityStore _visibilityStore;

    public TrayIcon(
        Action showIsland,
        Action toggleIsland,
        Action openSettings,
        Action exit,
        IUsageStore? usageStore = null,
        Backend.Monitoring.IActivityMonitor? activityMonitor = null,
        Backend.Settings.IProviderVisibilityStore? visibilityStore = null,
        Action? openTranscripts = null)
    {
        _usageStore = usageStore ?? (App.Instance?.Services?.GetService(typeof(IUsageStore)) as IUsageStore) ?? new UsageStore();
        _activityMonitor = activityMonitor ?? (App.Instance?.Services?.GetService(typeof(Backend.Monitoring.IActivityMonitor)) as Backend.Monitoring.IActivityMonitor) ?? new Backend.Monitoring.ActivityMonitor();
        _visibilityStore = visibilityStore ?? (App.Instance?.Services?.GetService(typeof(Backend.Settings.IProviderVisibilityStore)) as Backend.Settings.IProviderVisibilityStore) ?? new Backend.Settings.ProviderVisibilityStore();

        _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;

        _modernMenu = new ModernTrayMenu(
            showIsland: showIsland,
            toggleTransparentMode: toggleIsland,
            openSettings: openSettings,
            exit: exit,
            openDailyReport: () => Report.ReportWindow.Show(Report.ReportWindow.Kind.Daily),
            openWeeklyReport: () => Report.ReportWindow.Show(Report.ReportWindow.Kind.Weekly),
            openMonthlyReport: () => Report.ReportWindow.Show(Report.ReportWindow.Kind.Monthly),
            openTranscripts: openTranscripts,
            isTransparentModeQuery: () =>
            {
                var app = System.Windows.Application.Current;
                if (app is null || !app.Dispatcher.CheckAccess()) return false;
                return app.MainWindow is IslandWindow island && island.IsTransparentMode;
            });

        _icon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Agent Island",
        };
        // Left click pops the island up; right-click opens the modern tray flyout.
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                if (_modernMenu.IsOpen) _modernMenu.Hide();
                showIsland();
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                if (_modernMenu.IsOpen)
                {
                    _modernMenu.Hide();
                }
                else
                {
                    _modernMenu.ShowNearCursor();
                }
            }
        };

        _usageStore.PropertyChanged += OnDataChanged;
        _activityMonitor.PropertyChanged += OnDataChanged;
        _visibilityStore.PropertyChanged += OnDataChanged;
        Update();               // paints the first icon
        _icon.Visible = true;   // then show it
    }

    private void OnDataChanged(object? sender, PropertyChangedEventArgs e) =>
        _dispatcher.BeginInvoke(Update);

    /// Windows banner ("toast") in the tray corner — the system-notification
    /// half of an alarm, next to the foreground alarm window. On Win 10/11 a
    /// balloon tip renders as a native toast and lands in the Action Center.
    public void ShowBanner(string title, string message)
    {
        try
        {
            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText = string.IsNullOrWhiteSpace(message) ? title : message;
            _icon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.None;
            _icon.ShowBalloonTip(5000);
        }
        catch
        {
        }
    }

    private void Update()
    {
        var visibility = _visibilityStore;
        var usage = _usageStore;
        var monitor = _activityMonitor;

        double usage5h = 0;
        var worst = ActivityState.Idle;
        var usageParts = new List<string>();
        foreach (var provider in visibility.Enabled)
        {
            var state = monitor.StateFor(provider.ToTriggerTool());
            if (state > worst) worst = state;
            switch (provider)
            {
                case DisplayProvider.Claude:
                    usage5h = Math.Max(usage5h, usage.Claude.FiveHour.UsedPercent);
                    usageParts.Add("Claude " + Percent(usage.Claude.FiveHour.UsedPercent));
                    break;
                case DisplayProvider.Codex:
                    usage5h = Math.Max(usage5h, usage.Codex.FiveHour.UsedPercent);
                    usageParts.Add("Codex " + Percent(usage.Codex.FiveHour.UsedPercent));
                    break;
                default:
                    usageParts.Add(provider.DisplayName());
                    break;
            }
        }

        var visualKey = TrayIconRenderer.GetVisualStateKey(usage5h, worst);
        if (_rendered is null || _lastVisualKey is not { } last || !last.Equals(visualKey))
        {
            var next = TrayIconRenderer.Render(usage5h, worst);
            _icon.Icon = next;
            _rendered = next;
            _lastVisualKey = visualKey;
        }

        var joined = string.Join(" · ", usageParts);
        var status = StatusWord(worst);
        var nextText = status is null
            ? (joined.Length == 0 ? "Agent Island" : "Agent Island · " + joined)
            : $"Agent Island · {status}" + (joined.Length == 0 ? "" : " · " + joined);
        // NotifyIcon's native tooltip buffer is capped. Keep every provider in
        // the menu status, but never let a long enabled list break tray updates.
        var tooltip = nextText.Length > 63 ? nextText[..60] + "…" : nextText;
        if (!string.Equals(_icon.Text, tooltip, StringComparison.Ordinal))
        {
            _icon.Text = tooltip;
        }

        _modernMenu.UpdateStatus(joined, worst);
    }

    private static string Percent(double fraction) => $"{Core.Formatting.PercentInt(fraction)}%";

    private static string? StatusWord(ActivityState state) => state switch
    {
        ActivityState.AuthRequired => AgentIsland.UI.Localization.L10n.Tr("Needs attention"),
        ActivityState.RateLimited => AgentIsland.UI.Localization.L10n.Tr("Needs attention"),
        ActivityState.Stalled => AgentIsland.UI.Localization.L10n.Tr("Needs attention"),
        ActivityState.NeedsYou => AgentIsland.UI.Localization.L10n.Tr("Your turn"),
        ActivityState.Working => AgentIsland.UI.Localization.L10n.Tr("Running"),
        _ => null,
    };

    /// Windows toast-equivalent for the turn alarm's system notification.
    public void ShowBalloon(string title, string body)
    {
        try
        {
            _icon.ShowBalloonTip(5000, title, body, System.Windows.Forms.ToolTipIcon.None);
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (ReferenceEquals(Current, this)) Current = null;
        _usageStore.PropertyChanged -= OnDataChanged;
        _activityMonitor.PropertyChanged -= OnDataChanged;
        _visibilityStore.PropertyChanged -= OnDataChanged;
        _modernMenu.Destroy();
        _icon.Visible = false;
        _icon.Dispose();
        _rendered = null;
        _lastVisualKey = null;
    }
}

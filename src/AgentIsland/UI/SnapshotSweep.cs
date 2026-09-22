using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using AgentIsland.Core;
using AgentIsland.UI.Theme;

namespace AgentIsland.UI;

/// CI verification rig: AGENTISLAND_SNAPSHOT_DIR renders every major
/// surface — report cards AND the report window chrome (pager + calendar),
/// the island in compact/expanded and on every carousel page, the release
/// and guide cards, approval/transcript surfaces, every provider alarm dialog,
/// and all seven settings tabs — into the
/// directory and exits. Run under AGENTISLAND_DEMO=1 on a Windows runner,
/// this is how the port gets eyeballed frame by frame without a hand
/// install.
public static class SnapshotSweep
{
    private static readonly Color DarkBackdrop = Color.FromRgb(0x1A, 0x1C, 0x22);

    public static void Run(Application app, IslandWindow island, string dir)
    {
        Directory.CreateDirectory(dir);
        Try(() => RenderApprovalCards(dir));

        // The cost page hides by default; the sweep must SEE it (runner-local
        var screenPref = (App.Instance?.Services?.GetService(typeof(ScreenPref)) as ScreenPref) ?? new ScreenPref();
        Try(() => screenPref.ShowCostPage = true);
        var visibility = App.Instance?.Services?.GetService(typeof(Backend.Settings.IProviderVisibilityStore))
            as Backend.Settings.IProviderVisibilityStore;
        if (visibility is not null)
        {
            foreach (var provider in Providers.DisplayProviders.All) Try(() => visibility.SetEnabled(provider, true));
        }
        Try(() => (App.Instance?.Services?.GetService(typeof(Backend.Cost.ICostStore))
            as Backend.Cost.ICostStore)?.Refresh());

        // A wedged sweep must never leave the runner hanging for the job
        // timeout.
        var kill = new DispatcherTimer { Interval = TimeSpan.FromSeconds(150) };
        kill.Tick += (_, _) => app.Shutdown();
        kill.Start();

        string At(string name) => Path.Combine(dir, name);

        // Let demo data, layout, and the first paint settle; then walk the
        // surfaces one settle-beat at a time. SaveVisualSnapshot captures
        // ~0.6s AFTER the call — every island shot needs a full beat before
        // the next state mutation, or the shutter catches the next page.
        After(4.0, () =>
        {
            // Exercise the 2.6 top-bar forecast with two deterministic quota
            // samples. Settings/data live under the snapshot runner's
            // isolated AGENTISLAND_DATA_DIR and never touch normal prefs.
            Try(() =>
            {
                var usageStore = App.Instance?.Services?.GetService(typeof(Backend.Usage.IUsageStore))
                    as Backend.Usage.IUsageStore;
                var settings = App.Instance?.Services?.GetService(typeof(Backend.Settings.DepletionForecastSettingsStore))
                    as Backend.Settings.DepletionForecastSettingsStore;
                var tracker = App.Instance?.Services?.GetService(typeof(Backend.Usage.DepletionForecastTracker))
                    as Backend.Usage.DepletionForecastTracker;
                if (usageStore is not null && settings is not null && tracker is not null)
                {
                    var window = usageStore.Claude.FiveHour;
                    var now = DateTimeOffset.Now;
                    var remaining = Math.Clamp(1 - window.UsedPercent, 0.01, 0.99);
                    var key = Backend.Usage.DepletionForecastSeries.Quota(TriggerTool.Claude, window);
                    tracker.Observe(key, Math.Min(0.99, remaining + 0.10), now.AddMinutes(-5));
                    tracker.Observe(key, remaining, now);
                    settings.ThresholdPercent = 30;
                    settings.WindowMinutes = 15;
                    settings.Enabled = true;
                }
            });
            Try(() =>
            {
                Report.ReportWindow.WritePng(Report.ReportWindow.Kind.Daily, At("report-daily.png"));
                Report.ReportWindow.WritePng(Report.ReportWindow.Kind.Weekly, At("report-weekly.png"));
                Report.ReportWindow.WritePng(Report.ReportWindow.Kind.Monthly, At("report-monthly.png"));
            });
            island.SaveVisualSnapshot(At("island-compact.png"));
            After(1.0, () =>
            {
            island.PopUp();
            After(1.6, () =>
            {
                island.SaveVisualSnapshot(At("island-expanded.png"));
                After(1.0, () =>
                {
                screenPref.ForceForVerification(IslandScreen.Cost);
                After(0.9, () =>
                {
                    island.SaveVisualSnapshot(At("island-Cost.png"));
                    After(1.0, () =>
                    {
                    screenPref.ForceForVerification(IslandScreen.Overview);
                    After(1.0, () =>
                    {
                        island.SaveVisualSnapshot(At("island-overview.png"));
                        After(1.0, () =>
                        {
                        screenPref.ForceForVerification(IslandScreen.Usage);
                        Try(() => Report.ReportWindow.Show(Report.ReportWindow.Kind.Weekly));
                        After(1.5, () =>
                        {
                            RenderOpenWindow<Report.ReportWindow>(At("report-window-weekly.png"));
                            CloseOpenWindows<Report.ReportWindow>();
                            Try(() => RenderCalendarPopup(At("report-calendar.png")));
                            Try(WhatsNewWindow.Open);
                            After(1.3, () =>
                            {
                                RenderOpenWindow<WhatsNewWindow>(At("whatsnew.png"));
                                CloseOpenWindows<WhatsNewWindow>();
                                Try(WhatsNewWindow.OpenGuide);
                                After(1.3, () =>
                                {
                                    RenderOpenWindow<WhatsNewWindow>(At("guide.png"));
                                    CloseOpenWindows<WhatsNewWindow>();
                                    // Every provider's alarm dialog — each
                                    // must wear its OWN mark and accent.
                                    var tools = new[]
                                    {
                                        TriggerTool.Claude, TriggerTool.Codex, TriggerTool.Antigravity,
                                        TriggerTool.Grok, TriggerTool.Cursor, TriggerTool.DeepSeek,
                                    };
                                    var toolIndex = 0;
                                    void NextDialog()
                                    {
                                        if (toolIndex >= tools.Length)
                                        {
                                            Try(TranscriptWindow.ShowWindow);
                                            After(1.0, () =>
                                            {
                                                RenderOpenWindow<TranscriptWindow>(At("transcripts.png"));
                                                CloseOpenWindows<TranscriptWindow>();
                                                SettingsWindow.SnapshotAllTabs(dir, app.Shutdown);
                                            });
                                            return;
                                        }
                                        var tool = tools[toolIndex];
                                        toolIndex++;
                                        Try(() => IslandDialog.Show(
                                            tool,
                                            AgentIsland.UI.Localization.L10n.Tr("Your turn"),
                                            AgentIsland.UI.Localization.L10n.Tr("A thread finished — Agent Island opens an alarm window so you can reply."),
                                            meta: new[]
                                            {
                                                (AgentIsland.UI.Localization.L10n.Tr("Alarm thread"), "Agent Island Windows"),
                                                (AgentIsland.UI.Localization.L10n.Tr("Alarm project"), "Agent Island"),
                                            },
                                            primaryLabel: AgentIsland.UI.Localization.L10n.Tr("Open"),
                                            secondaryLabel: AgentIsland.UI.Localization.L10n.Tr("I know")));
                                        After(1.1, () =>
                                        {
                                            RenderOpenWindow<IslandDialog>(At($"dialog-{tool}".ToLowerInvariant() + ".png"));
                                            CloseOpenWindows<IslandDialog>();
                                            NextDialog();
                                        });
                                    }
                                    NextDialog();
                                });
                            });
                        });
                        });
                    });
                    });
                });
                });
            });
            });
        });
    }

    private static void RenderApprovalCards(string dir)
    {
        var interactionRoot = Path.Combine(dir, "interaction-preview");
        var channel = new Backend.Interaction.FileInteractionChannel(interactionRoot);
        var coordinator = new Backend.Interaction.ApprovalCoordinator(channel);
        var requests = new[]
        {
            new Core.Interaction.ApprovalRequest(
                "snapshot-permission", TriggerTool.Claude, Core.Interaction.PromptKind.Permission,
                "Bash", "Remove-Item ./build -Recurse", null, Array.Empty<Core.Interaction.PromptOption>(),
                null, "snapshot", "snapshot", Environment.CurrentDirectory, DateTimeOffset.UtcNow,
                Core.Interaction.CommandRiskLevel.Warn, "This command removes files"),
            new Core.Interaction.ApprovalRequest(
                "snapshot-question", TriggerTool.Claude, Core.Interaction.PromptKind.Question,
                "AskUserQuestion", null, "Which queue should be used?",
                new[] { new Core.Interaction.PromptOption("Existing queue", "Reuse the current queue") },
                null, "snapshot", "snapshot", Environment.CurrentDirectory, DateTimeOffset.UtcNow,
                Core.Interaction.CommandRiskLevel.None, null),
            new Core.Interaction.ApprovalRequest(
                "snapshot-plan", TriggerTool.Claude, Core.Interaction.PromptKind.Plan,
                "ExitPlanMode", null, null, Array.Empty<Core.Interaction.PromptOption>(),
                "# Plan\n\n1. Make the change\n2. Run the tests", "snapshot", "snapshot",
                Environment.CurrentDirectory, DateTimeOffset.UtcNow,
                Core.Interaction.CommandRiskLevel.None, null),
        };
        foreach (var request in requests)
        {
            channel.Publish(request);
            coordinator.Refresh();
            var card = new Interaction.ApprovalCard(coordinator) { Width = 430 };
            card.Refresh();
            card.Measure(new Size(430, 600));
            card.Arrange(new Rect(0, 0, 430, card.DesiredSize.Height));
            card.UpdateLayout();
            RenderElement(card, Path.Combine(dir, $"approval-{request.Kind.ToString().ToLowerInvariant()}.png"));
            channel.Clear(request.Id);
        }
    }

    private static void After(double seconds, Action action)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
    }

    private static void Try(Action action)
    {
        try
        {
            action();
        }
        catch
        {
        }
    }

    /// Render the first open window of the type: its content over a dark
    /// backdrop, so borderless white-on-transparent chrome stays readable.
    private static void RenderOpenWindow<TWindow>(string path) where TWindow : Window
    {
        Try(() =>
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is not TWindow || window.Content is not FrameworkElement root) continue;
                RenderElement(root, path);
                return;
            }
        });
    }

    private static void CloseOpenWindows<TWindow>() where TWindow : Window
    {
        Try(() =>
        {
            var open = new List<Window>();
            foreach (Window window in Application.Current.Windows)
            {
                if (window is TWindow) open.Add(window);
            }
            foreach (var window in open) window.Close();
        });
    }

    /// The report calendar never shows unless clicked — render its content
    /// tree unrooted at the popup's natural size.
    private static void RenderCalendarPopup(string path)
    {
        var popup = new Report.ReportCalendarPopup(DateTime.Today.AddMonths(-3), _ => { });
        if (popup.Child is not FrameworkElement child) return;
        child.Measure(new Size(300, 400));
        child.Arrange(new Rect(child.DesiredSize));
        child.UpdateLayout();
        RenderElement(child, path);
    }

    private static void RenderElement(FrameworkElement root, string path)
    {
        var w = (int)Math.Ceiling(root.ActualWidth);
        var h = (int)Math.Ceiling(root.ActualHeight);
        if (w <= 0 || h <= 0) return;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(IslandColors.Brush(DarkBackdrop), null, new Rect(0, 0, w, h));
            dc.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, w, h));
        }
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}

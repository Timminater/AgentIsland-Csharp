using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using AgentIsland.Core;
using AgentIsland.UI.Theme;
using AgentIsland.Core.Usage;
using AgentIsland.Backend.Usage;
using AgentIsland.UI.Localization;

namespace AgentIsland.UI;

/// The peek-state glance readout: `32% · 2h` — 5h used percentage tinted in
/// the provider color, reset countdown in quiet white. Rendering rules match
/// the macOS NotchPeekPill: keep the last good value during refresh, `—%` on
/// error, dim window-length fallback when no live countdown exists.
public sealed class NotchPeekPill : TextBlock
{
    private TriggerTool _tool = TriggerTool.Claude;
    private readonly AgentIsland.Backend.Settings.QuotaDisplayModeStore _displayModeStore;

    public NotchPeekPill() : this(null) { }

    public NotchPeekPill(AgentIsland.Backend.Settings.QuotaDisplayModeStore? displayModeStore)
    {
        _displayModeStore = displayModeStore
            ?? (App.Instance?.Services?.GetService(typeof(AgentIsland.Backend.Settings.QuotaDisplayModeStore)) as AgentIsland.Backend.Settings.QuotaDisplayModeStore)
            ?? new AgentIsland.Backend.Settings.QuotaDisplayModeStore();
        FontFamily = new FontFamily("Cascadia Mono, Consolas");
        FontSize = 11;
        FontWeight = FontWeights.SemiBold;
        VerticalAlignment = VerticalAlignment.Center;
    }

    public TriggerTool Tool
    {
        get => _tool;
        set => _tool = value;
    }

    public bool Mirrored { get; set; }

    public void Update(
        WindowUsage usage,
        bool loading,
        AgentIsland.Backend.Settings.AlertSeverity severity = AgentIsland.Backend.Settings.AlertSeverity.None,
        DepletionForecast? forecast = null)
    {
        Inlines.Clear();
        ToolTip = null;
        var tint = severity switch
        {
            AgentIsland.Backend.Settings.AlertSeverity.Critical => IslandColors.AlertRed,
            AgentIsland.Backend.Settings.AlertSeverity.Warning => IslandColors.AlertAmber,
            _ => IslandColors.For(_tool),
        };

        if (usage.HasError && usage.UsedPercent == 0)
        {
            Inlines.Add(Dim("—%", 0.40));
            return;
        }
        if (loading && usage.UsedPercent == 0 && usage.ResetAt is null)
        {
            Inlines.Add(Dim("…", 0.55));
            return;
        }

        // The right-hand pill mirrors: countdown first, percent hugging the
        // logo — exactly like the macOS bar.
        var percentText = $"{Math.Round(_displayModeStore.DisplayValue(usage.UsedPercent))}%";
        var now = DateTimeOffset.Now;
        var resetCountdown = usage.ResetAt is { } resetAt && resetAt > now
            ? CompactCountdown(resetAt - now)
            : null;
        var depletionCountdown = forecast is not null
            ? "≈" + CompactCountdown(forecast.Remaining)
            : null;
        var mirrored = Mirrored;
        // The no-countdown placeholder names the window's REAL period.
        var periodTag = Charts.ChartTile.PeriodLabel(usage, "5h");

        if (mirrored)
        {
            if (depletionCountdown is not null) Inlines.Add(ForecastRun(depletionCountdown + " · "));
            else if (resetCountdown is not null) Inlines.Add(Dim(resetCountdown + " · ", 0.70));
            else Inlines.Add(Dim(periodTag + " · ", 0.40));
            Inlines.Add(new Run(percentText) { Foreground = IslandColors.Brush(tint) });
            if (severity != AgentIsland.Backend.Settings.AlertSeverity.None)
            {
                Inlines.Add(new Run(" ⚠") { Foreground = IslandColors.Brush(tint) });
            }
        }
        else
        {
            if (severity != AgentIsland.Backend.Settings.AlertSeverity.None)
            {
                Inlines.Add(new Run("⚠ ") { Foreground = IslandColors.Brush(tint) });
            }
            Inlines.Add(new Run(percentText) { Foreground = IslandColors.Brush(tint) });
            if (depletionCountdown is not null) Inlines.Add(ForecastRun(" · " + depletionCountdown));
            else if (resetCountdown is not null) Inlines.Add(Dim(" · " + resetCountdown, 0.70));
            else Inlines.Add(Dim(" · " + periodTag, 0.40));
        }

        ToolTip = forecast is null ? null : ForecastTooltip(forecast);
    }

    /// Renders an account balance for providers such as DeepSeek that do not
    /// expose a percentage-based quota window. The compact pill must still
    /// occupy the same visual slot: a fresh balance is shown as-is, a first
    /// request shows an ellipsis, and an unavailable/error state keeps a
    /// visible marker instead of collapsing to an empty flank.
    ///
    /// DeepSeek also bills off-peak at half price, so the balance carries the
    /// live billing phase as a tinted dot in front of the amount. The pill's
    /// slot is only ~104 DIP wide: naming the phase in words here overflowed
    /// it and clipped the amount down to its cents, so the words live in the
    /// tooltip and on the island's expanded panel instead.
    public void UpdateBalance(
        string? balanceText,
        bool loading,
        bool unavailable = false,
        DepletionForecast? forecast = null)
    {
        Inlines.Clear();
        ToolTip = null;

        var tint = unavailable ? IslandColors.AlertRed : IslandColors.For(_tool);
        var hasAmount = !string.IsNullOrWhiteSpace(balanceText);
        var isDeepSeek = _tool == TriggerTool.DeepSeek;
        // The dot only rides an actual amount: a phase marker beside a loading
        // ellipsis is noise, and it is the amount that has to fit the slot.
        // When the estimate is visible it takes the phase dot's scarce slot;
        // peak/off-peak remains fully named in the tooltip.
        var phase = isDeepSeek && hasAmount && forecast is null ? PhaseMark() : (Color?)null;
        var forecastText = forecast is null ? null : "≈" + CompactCountdown(forecast.Remaining);

        if (Mirrored)
        {
            if (forecastText is not null) Inlines.Add(ForecastRun(forecastText + " · "));
            if (hasAmount)
            {
                Inlines.Add(new Run(balanceText!) { Foreground = IslandColors.Brush(tint) });
            }
            else
            {
                Inlines.Add(Dim(loading ? "…" : "—", loading ? 0.55 : 0.40));
            }
            if (unavailable)
            {
                Inlines.Add(new Run(" ⚠") { Foreground = IslandColors.Brush(tint) });
            }
            if (phase is { } leftPhase)
            {
                Inlines.Add(new Run(" ●") { Foreground = IslandColors.Brush(leftPhase) });
            }
        }
        else
        {
            if (phase is { } rightPhase)
            {
                Inlines.Add(new Run("● ") { Foreground = IslandColors.Brush(rightPhase) });
            }
            if (unavailable)
            {
                Inlines.Add(new Run("⚠ ") { Foreground = IslandColors.Brush(tint) });
            }
            Inlines.Add(hasAmount
                ? new Run(balanceText!) { Foreground = IslandColors.Brush(tint) }
                : Dim(loading ? "…" : "—", loading ? 0.55 : 0.40));
            if (forecastText is not null) Inlines.Add(ForecastRun(" · " + forecastText));
        }

        if (isDeepSeek)
        {
            var phaseTip = DeepSeekPeakHoursText.Tag(DateTimeOffset.Now)
                + " · " + DeepSeekPeakHoursText.Long(DateTimeOffset.Now);
            ToolTip = forecast is null ? phaseTip : phaseTip + "\n" + ForecastTooltip(forecast);
        }
        else ToolTip = forecast is null ? null : ForecastTooltip(forecast);
    }

    /// Live billing-phase colour: teal while off-peak, amber while peak.
    private static Color PhaseMark() =>
        AgentIsland.Providers.Usage.DeepSeek.DeepSeekPeakHours.IsPeak(DateTimeOffset.Now)
            ? IslandColors.AlertAmber
            : IslandColors.LiveTeal;

    public static string CompactCountdown(TimeSpan remaining)
    {
        // Day-unit first: a weekly window reads "6d", never "150h". Then Nh
        // when >= 1h (floored — 107m reads "1h"), Nm under 1h. Never mixed:
        // "1h 47m" is too noisy for a glance pill.
        if (remaining.TotalDays >= 2) return $"{(int)remaining.TotalDays}d";
        if (remaining.TotalHours >= 1) return $"{(int)remaining.TotalHours}h";
        return $"{Math.Max(1, (int)Math.Round(remaining.TotalMinutes))}m";
    }

    private static Run Dim(string text, double opacity) => new(text)
    {
        Foreground = IslandColors.Brush(IslandColors.White(opacity)),
    };

    private static Run ForecastRun(string text) => new(text)
    {
        Foreground = IslandColors.Brush(IslandColors.AlertAmber),
    };

    private static string ForecastTooltip(DepletionForecast forecast) =>
        L10n.TrFormat(
            "Estimated empty in {0}, based on the last {1} minutes",
            CompactCountdown(forecast.Remaining),
            forecast.WindowMinutes);
}

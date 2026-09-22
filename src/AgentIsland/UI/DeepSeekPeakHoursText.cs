using AgentIsland.Providers.Usage.DeepSeek;

namespace AgentIsland.UI;

/// Presentation helper for DeepSeek's peak / off-peak billing phase. The
/// schedule itself lives in DeepSeekPeakHours; this type only decides how the
/// phase reads in each surface — a one-word tag on the island's peek pill, a
/// caption with the discount and the countdown on the panel column and the
/// Settings row.
internal static class DeepSeekPeakHoursText
{
    /// The bare phase name, for the tightest surface (the peek pill).
    public static string Tag(DateTimeOffset now) =>
        DeepSeekPeakHours.IsPeak(now) ? L10n.Tr("peak") : L10n.Tr("off-peak");

    /// Phase plus discount — the Settings row and the panel status line.
    public static string Short(DateTimeOffset now) =>
        DeepSeekPeakHours.IsPeak(now)
            ? L10n.Tr("Peak hours")
            : L10n.Tr("Off-peak") + " · " + L10n.Tr("50% off");

    /// Phase, discount and the time the phase flips, on the user's own clock.
    public static string Long(DateTimeOffset now)
    {
        var transition = DeepSeekPeakHours.ToLocal(DeepSeekPeakHours.NextTransition(now));
        var when = DeepSeekPeakHours.TransitionIsAnotherDay(now)
            ? transition.ToString("ddd HH:mm")
            : transition.ToString("HH:mm");
        return Short(now) + " · " + L10n.TrFormat(
            "{0} → {1}",
            DeepSeekPeakHoursBar.FormatCountdown(DeepSeekPeakHours.TimeUntilTransition(now)),
            when);
    }

    /// The tooltip for the panel's day bar: the published windows, plus the
    /// same windows on the clock the bar is drawn with.
    public static string ScheduleTooltip()
    {
        var official = L10n.Tr(
            "DeepSeek bills peak rates Monday–Friday 09:00–12:00 and 14:00–18:00 Beijing time; every other hour is off-peak at half price.");
        var local = DeepSeekPeakHours.LocalOffset == DeepSeekPeakHours.BeijingOffset
            ? null
            : L10n.TrFormat(
                "Shown on your clock: {0}.",
                string.Join(
                    ", ",
                    DeepSeekPeakHours.PeakWindowsForDay(DateTimeOffset.Now)
                        .Select(window => $"{Clock(window.Start)}–{Clock(window.End)}")));
        return local is null ? official : official + " " + local;
    }

    /// A fractional hour-of-day as HH:mm.
    internal static string Clock(double hourOfDay)
    {
        var total = (int)Math.Round(hourOfDay * 60);
        return $"{total / 60:00}:{total % 60:00}";
    }
}

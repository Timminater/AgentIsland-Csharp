namespace AgentIsland.Providers.Usage.DeepSeek;

/// <summary>
/// DeepSeek's published peak / off-peak billing schedule for the API.
///
/// The official pricing note reads: "Off-peak rates are half of peak rates.
/// Peak hours are Beijing time Monday to Friday 9:00-12:00 and 14:00-18:00;
/// all other times are off-peak." The windows are therefore *defined* in the
/// Beijing offset (UTC+8) and nowhere else — weekends never carry a peak
/// window — but every query here is answered for the caller's own time zone,
/// so the bars and captions can render the schedule on the clock the user
/// actually reads.
///
/// This type is pure schedule math with no UI and no clock of its own, so the
/// bars, pills and captions all agree on one source of truth.
/// </summary>
public static class DeepSeekPeakHours
{
    /// <summary>Beijing standard time — the reference zone of the price list.</summary>
    public static readonly TimeSpan BeijingOffset = TimeSpan.FromHours(8);

    /// <summary>The zone the peak windows are published in.</summary>
    public static readonly TimeZoneInfo BeijingZone = TimeZoneInfo.CreateCustomTimeZone(
        "DeepSeek-Beijing",
        BeijingOffset,
        "Beijing time",
        "Beijing time");

    /// <summary>Peak-window discount multiplier DeepSeek bills off-peak at.</summary>
    public const decimal OffPeakRate = 0.5m;

    /// <summary>Peak windows in Beijing wall-clock time, as [startHour, endHour).</summary>
    private static readonly (int Start, int End)[] BeijingPeakWindows =
    {
        (9, 12),
        (14, 18),
    };

    /// <summary>
    /// The offset to render the schedule in — the machine's by default. Read
    /// live rather than cached at startup, so a DST flip mid-session moves the
    /// bar with the clock; tests pin it to prove the conversion.
    /// </summary>
    public static TimeSpan LocalOffset
    {
        get => _localOffsetOverride ?? DateTimeOffset.Now.Offset;
        set => _localOffsetOverride = value;
    }

    private static TimeSpan? _localOffsetOverride;

    /// <summary>Drops the test override and follows the machine's clock again.</summary>
    public static void ResetLocalOffset() => _localOffsetOverride = null;

    /// <summary>Projects an instant into a zone's wall clock.</summary>
    public static DateTimeOffset InZone(DateTimeOffset moment, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(moment, zone);

    /// <summary>Projects an instant onto the clock the schedule is rendered in.</summary>
    public static DateTimeOffset ToLocal(DateTimeOffset moment)
    {
        var offset = LocalOffset;
        return moment.ToOffset(offset);
    }

    /// <summary>The Beijing-local timestamp a given instant maps to.</summary>
    public static DateTimeOffset InBeijing(DateTimeOffset moment) => InZone(moment, BeijingZone);

    /// <summary>
    /// True when the instant falls inside a published peak window. Weekends
    /// (Beijing time) are always off-peak.
    /// </summary>
    public static bool IsPeak(DateTimeOffset moment)
    {
        var beijing = InBeijing(moment);
        if (beijing.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        foreach (var (start, end) in BeijingPeakWindows)
        {
            if (beijing.Hour >= start && beijing.Hour < end) return true;
        }
        return false;
    }

    public static bool IsOffPeak(DateTimeOffset moment) => !IsPeak(moment);

    /// <summary>
    /// The instant the current peak/off-peak phase ends — the next published
    /// window boundary. Returned as a real instant, so callers render it in
    /// whichever zone they are showing.
    /// </summary>
    public static DateTimeOffset NextTransition(DateTimeOffset moment)
    {
        var beijing = InBeijing(moment);
        foreach (var (start, end) in BeijingPeakWindows)
        {
            if (beijing.Hour >= start && beijing.Hour < end)
            {
                return ToInstant(beijing.Date, end);
            }
        }
        // Off-peak: walk forward to the next window opening — today's later
        // window, tomorrow's first, or Monday's after a weekend. The loop is
        // bounded by the weekend rule and always lands on a future instant.
        for (var dayOffset = 0; dayOffset <= 7; dayOffset++)
        {
            var date = beijing.Date.AddDays(dayOffset);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            foreach (var (start, _) in BeijingPeakWindows)
            {
                var candidate = ToInstant(date, start);
                if (candidate > moment) return candidate;
            }
        }
        return ToInstant(beijing.Date.AddDays(8), BeijingPeakWindows[0].Start);
    }

    /// <summary>Time remaining in the current phase.</summary>
    public static TimeSpan TimeUntilTransition(DateTimeOffset moment) =>
        NextTransition(moment) - moment;

    /// <summary>
    /// One hour of the rendered day, with its peak/off-peak state. The 24
    /// entries drive the day bar: index 0 is 00:00 on the local clock. An hour
    /// takes the state of its midpoint, which is exact for every zone whose
    /// offset is a whole number of hours and can only differ for the half-hour
    /// offsets DeepSeek does not bill in.
    /// </summary>
    public static IReadOnlyList<DeepSeekPeakHourBand> DayBands(DateTimeOffset moment)
    {
        var local = ToLocal(moment);
        var bands = new DeepSeekPeakHourBand[24];
        for (var hour = 0; hour < 24; hour++)
        {
            // The midpoint must carry the RENDERED offset, not the machine's:
            // a bare DateTime would be read back in the machine zone and shift
            // every band by the difference between the two.
            var mid = new DateTimeOffset(
                local.Date.AddHours(hour).AddMinutes(30),
                local.Offset);
            bands[hour] = new DeepSeekPeakHourBand(hour, IsPeak(mid));
        }
        return bands;
    }

    /// <summary>
    /// The peak windows of the rendered day, expressed as local hour ranges.
    /// A Beijing weekend window simply does not exist, so a local day that
    /// covers only weekend hours comes back empty.
    /// </summary>
    public static IReadOnlyList<(double Start, double End)> PeakWindowsForDay(DateTimeOffset moment)
    {
        // Keep every value a DateTimeOffset: comparing a DateTime against one
        // silently re-reads the DateTime in the MACHINE zone, which shifted
        // the whole schedule by the machine/rendered offset difference.
        var local = ToLocal(moment);
        var dayStart = new DateTimeOffset(local.Date, local.Offset);
        var dayEnd = dayStart.AddDays(1);
        var windows = new List<(double Start, double End)>();

        // Any local day overlaps at most two Beijing calendar days, and the
        // zone shift is bounded well inside ±2 days.
        for (var dayOffset = -2; dayOffset <= 2; dayOffset++)
        {
            var beijingDate = InBeijing(moment).Date.AddDays(dayOffset);
            if (beijingDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            foreach (var (start, end) in BeijingPeakWindows)
            {
                var from = ToLocal(ToInstant(beijingDate, start));
                var to = ToLocal(ToInstant(beijingDate, end));
                var clampedStart = from < dayStart ? dayStart : from;
                var clampedEnd = to > dayEnd ? dayEnd : to;
                if (clampedEnd <= clampedStart) continue;
                windows.Add((
                    (clampedStart - dayStart).TotalHours,
                    (clampedEnd - dayStart).TotalHours));
            }
        }
        windows.Sort((left, right) => left.Start.CompareTo(right.Start));
        return windows;
    }

    /// <summary>
    /// How much of the rendered day is off-peak, 0-1. A weekend is 1.0; a
    /// plain weekday is 17/24 (the two peak windows total 7h).
    /// </summary>
    public static double OffPeakShare(DateTimeOffset moment)
    {
        var peakHours = PeakWindowsForDay(moment).Sum(window => window.End - window.Start);
        return Math.Clamp((24 - peakHours) / 24.0, 0, 1);
    }

    /// <summary>
    /// True when the transition lands on a different rendered day than the
    /// given instant — a caption then names the weekday instead of a bare hour.
    /// </summary>
    public static bool TransitionIsAnotherDay(DateTimeOffset moment) =>
        ToLocal(NextTransition(moment)).Date != ToLocal(moment).Date;

    /// <summary>True when the rendered day is a Beijing weekend (all off-peak).</summary>
    public static bool IsWeekend(DateTimeOffset moment)
    {
        var local = ToLocal(moment);
        var windows = PeakWindowsForDay(moment);
        if (windows.Count > 0) return false;
        // A windowless local day is a weekend only when the Beijing day it
        // sits in is one; a weekday whose windows both fall outside the local
        // day (a far-east offset) must not be called a weekend.
        return InBeijing(moment).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    /// <summary>The label of the zone the schedule is rendered in.</summary>
    public static string LocalZoneLabel => LocalOffset == BeijingOffset
        ? "Beijing time"
        : $"UTC{(LocalOffset < TimeSpan.Zero ? "-" : "+")}{Math.Abs(LocalOffset.TotalHours):0.##}";

    /// <summary>Beijing wall-clock [date + hour] as a real instant.</summary>
    private static DateTimeOffset ToInstant(DateTime date, int hour) =>
        new(date.Year, date.Month, date.Day, hour, 0, 0, BeijingOffset);
}

/// <summary>One hour of the rendered billing day.</summary>
public sealed record DeepSeekPeakHourBand(int Hour, bool IsPeak);

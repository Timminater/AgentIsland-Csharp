namespace AgentIsland.Core;

public static class Formatting
{
    /// Single-unit compact duration: "45s", "12m", "3h", "4d" — matches the
    /// macOS Duration.compact used in reset countdowns and captions.
    public static string CompactDuration(TimeSpan span)
    {
        var seconds = Math.Max(0, span.TotalSeconds);
        if (seconds < 60) return $"{(int)seconds}s";
        if (seconds < 3600) return $"{(int)(seconds / 60)}m";
        if (seconds < 86400) return $"{(int)(seconds / 3600)}h";
        return $"{(int)(seconds / 86400)}d";
    }

    /// The one percent readout in the app: a 0-1 fraction as whole points.
    ///
    /// Away-from-zero, not .NET's default banker's rounding. Swift's
    /// `WindowUsage.percentInt` is `Int((usedPercent * 100).rounded())`, which
    /// rounds .5 up; `Math.Round` alone rounds it to even, so an exact 0.125
    /// printed 12% on Windows and 13% on macOS — and the tray, the panel and
    /// the report cards each have to agree with the other platform AND with
    /// each other.
    public static int PercentInt(double fraction) =>
        (int)Math.Round(fraction * 100, MidpointRounding.AwayFromZero);

    /// "$146.61" / "$1,510.80" — invariant thousands separators.
    public static string Money(double dollars) =>
        "$" + dollars.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);

    /// "941", "12.4k", "211.2M", "2.17B".
    public static string CompactTokens(long tokens)
    {
        // Boundaries are 999_500, not 1_000_000: at 999_500 the "0"-rounding
        // below already renders 1000, so promote to the next unit there to
        // print "1.0M" instead of "1000k".
        return tokens switch
        {
            < 1_000 => tokens.ToString(System.Globalization.CultureInfo.InvariantCulture),
            < 999_500 => Trim(tokens / 1_000.0) + "k",
            < 999_500_000 => Trim(tokens / 1_000_000.0) + "M",
            _ => Trim(tokens / 1_000_000_000.0) + "B",
        };

        static string Trim(double value) =>
            value.ToString(value >= 100 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// Long-form countdown for Dutch or English interface captions.
    public static string LongCountdown(TimeSpan until, bool dutch)
    {
        var seconds = Math.Max(0, until.TotalSeconds);
        if (dutch)
        {
            if (seconds < 60) return "binnen 1 min";
            if (seconds < 3600) return $"over {(int)(seconds / 60)} min";
            if (seconds < 86400) return $"over {(int)(seconds / 3600)} uur";
            return $"over {(int)(seconds / 86400)} dagen";
        }
        if (seconds < 60) return "under 1m";
        if (seconds < 3600) return $"in {(int)(seconds / 60)}m";
        if (seconds < 86400) return $"in {(int)(seconds / 3600)}h";
        return $"in {(int)(seconds / 86400)}d";
    }

    /// Relative sync label with second granularity under a minute.
    public static string RelativeAgo(TimeSpan since, bool dutch)
    {
        var seconds = Math.Max(0, since.TotalSeconds);
        if (seconds < 5) return dutch ? "zojuist" : "just now";
        if (seconds < 60)
        {
            var s = (int)seconds;
            return dutch ? $"{s} sec geleden" : $"{s}s ago";
        }
        if (seconds < 3600)
        {
            var m = (int)(seconds / 60);
            return dutch ? $"{m} min geleden" : $"{m}m ago";
        }
        var h = (int)(seconds / 3600);
        return dutch ? $"{h} uur geleden" : $"{h}h ago";
    }
}

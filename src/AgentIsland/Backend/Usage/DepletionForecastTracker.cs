namespace AgentIsland.Backend.Usage;

using AgentIsland.Core;
using AgentIsland.Core.Usage;

public static class DepletionForecastSeries
{
    public static string Quota(TriggerTool tool, WindowUsage usage) =>
        $"{tool.RawValue()}:quota:{usage.ResetAt?.ToUnixTimeSeconds().ToString() ?? "rolling"}:{usage.PeriodSeconds?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}";

    public static string Balance(string currency) =>
        $"deepseek:balance:{currency.Trim().ToUpperInvariant()}";
}

/// A rate-based estimate derived only from observed quota/balance decline.
/// RemainingValue is unit-agnostic: fractions and currency amounts follow the
/// same maths as long as each series keeps one unit.
public sealed record DepletionForecast(
    TimeSpan Remaining,
    DateTimeOffset ExhaustsAt,
    int WindowMinutes,
    bool Automatic);

public sealed class DepletionForecastTracker
{
    private sealed record Sample(DateTimeOffset At, double Remaining);

    private readonly object _gate = new();
    private readonly Dictionary<string, List<Sample>> _series = new(StringComparer.Ordinal);
    private static readonly TimeSpan Retention = TimeSpan.FromMinutes(65);
    private const double Epsilon = 0.000001;

    public void Observe(string seriesKey, double remainingValue, DateTimeOffset observedAt)
    {
        if (string.IsNullOrWhiteSpace(seriesKey) || !double.IsFinite(remainingValue)) return;
        remainingValue = Math.Max(0, remainingValue);

        lock (_gate)
        {
            if (!_series.TryGetValue(seriesKey, out var samples))
            {
                samples = new List<Sample>();
                _series[seriesKey] = samples;
            }

            if (samples.Count > 0)
            {
                var last = samples[^1];
                if (observedAt < last.At) return;
                if (observedAt == last.At)
                {
                    samples[^1] = new Sample(observedAt, remainingValue);
                    return;
                }

                // A quota reset or balance top-up starts a new burn segment;
                // joining both sides would manufacture a negative rate.
                if (remainingValue > last.Remaining + Math.Max(Epsilon, last.Remaining * 0.001))
                {
                    samples.Clear();
                }
            }

            samples.Add(new Sample(observedAt, remainingValue));
            var cutoff = observedAt - Retention;
            samples.RemoveAll(sample => sample.At < cutoff);
        }
    }

    public DepletionForecast? Estimate(
        string seriesKey,
        DateTimeOffset now,
        int configuredWindowMinutes,
        bool providerWorking,
        int minimumWindowMinutes = 0)
    {
        var automatic = configuredWindowMinutes == 0;
        // In automatic mode a paused/idle local session must not keep showing
        // an old burn rate as though usage were still falling.
        if (automatic && !providerWorking) return null;

        var windowMinutes = Math.Max(
            automatic ? 15 : configuredWindowMinutes,
            Math.Max(0, minimumWindowMinutes));
        if (windowMinutes <= 0) return null;

        List<Sample> selected;
        lock (_gate)
        {
            if (!_series.TryGetValue(seriesKey, out var samples) || samples.Count < 2) return null;
            var latest = samples[^1];
            var cutoff = latest.At.AddMinutes(-windowMinutes);
            selected = samples.Where(sample => sample.At >= cutoff).ToList();
            // Provider polls land a few seconds either side of the configured
            // cadence. Include a near-boundary anchor so a 5-minute window
            // still works when two polls are 5m03s apart.
            var firstSelected = samples.FindIndex(sample => sample.At >= cutoff);
            if (firstSelected > 0)
            {
                var anchor = samples[firstSelected - 1];
                var tolerance = TimeSpan.FromSeconds(Math.Min(120, windowMinutes * 15));
                if (cutoff - anchor.At <= tolerance) selected.Insert(0, anchor);
            }
        }

        if (selected.Count < 2) return null;
        var first = selected[0];
        var last = selected[^1];
        var elapsedSeconds = (last.At - first.At).TotalSeconds;
        if (elapsedSeconds < 30) return null;

        // A fixed-window forecast also goes quiet after a complete unchanged
        // sample interval. This is the pause detector when no activity-aware
        // automatic mode was selected.
        var previous = selected[^2];
        if (last.Remaining >= previous.Remaining - Epsilon) return null;

        var consumed = first.Remaining - last.Remaining;
        if (consumed <= Epsilon) return null;
        var perSecond = consumed / elapsedSeconds;
        if (!double.IsFinite(perSecond) || perSecond <= 0) return null;

        var secondsFromLatest = last.Remaining / perSecond;
        if (!double.IsFinite(secondsFromLatest) || secondsFromLatest <= 0) return null;
        var exhaustsAt = last.At.AddSeconds(secondsFromLatest);
        var remaining = exhaustsAt - now;
        if (remaining <= TimeSpan.Zero || remaining > TimeSpan.FromDays(365)) return null;
        return new DepletionForecast(remaining, exhaustsAt, windowMinutes, automatic);
    }

    public void Clear(string? seriesKey = null)
    {
        lock (_gate)
        {
            if (seriesKey is null) _series.Clear();
            else _series.Remove(seriesKey);
        }
    }
}

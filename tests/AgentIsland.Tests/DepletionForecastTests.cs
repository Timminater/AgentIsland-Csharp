using AgentIsland.Backend.Usage;

namespace AgentIsland.Tests;

public class DepletionForecastTests
{
    [Fact]
    public void FallingQuotaProducesTimeToZero()
    {
        var tracker = new DepletionForecastTracker();
        var start = new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);
        tracker.Observe("codex:window-a", 0.30, start);
        tracker.Observe("codex:window-a", 0.20, start.AddMinutes(5));

        var result = tracker.Estimate("codex:window-a", start.AddMinutes(5), 15, providerWorking: true);

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.FromMinutes(10), result!.Remaining);
        Assert.Equal(15, result.WindowMinutes);
        Assert.False(result.Automatic);
    }

    [Fact]
    public void AutomaticForecastHidesWhileProviderIsPaused()
    {
        var tracker = new DepletionForecastTracker();
        var start = new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);
        tracker.Observe("claude:window-a", 0.40, start);
        tracker.Observe("claude:window-a", 0.30, start.AddMinutes(5));

        Assert.Null(tracker.Estimate(
            "claude:window-a", start.AddMinutes(5), configuredWindowMinutes: 0, providerWorking: false));
        var active = tracker.Estimate(
            "claude:window-a", start.AddMinutes(5), configuredWindowMinutes: 0, providerWorking: true);
        Assert.NotNull(active);
        Assert.True(active!.Automatic);
        Assert.Equal(15, active.WindowMinutes);
    }

    [Fact]
    public void UnchangedLatestSampleSuppressesFixedForecast()
    {
        var tracker = new DepletionForecastTracker();
        var start = new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);
        tracker.Observe("cursor:cycle", 0.40, start);
        tracker.Observe("cursor:cycle", 0.30, start.AddMinutes(5));
        tracker.Observe("cursor:cycle", 0.30, start.AddMinutes(10));

        Assert.Null(tracker.Estimate(
            "cursor:cycle", start.AddMinutes(10), configuredWindowMinutes: 30, providerWorking: true));
    }

    [Fact]
    public void ResetOrTopUpStartsANewSeriesSegment()
    {
        var tracker = new DepletionForecastTracker();
        var start = new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);
        tracker.Observe("deepseek:usd", 2.0, start);
        tracker.Observe("deepseek:usd", 1.0, start.AddMinutes(5));
        tracker.Observe("deepseek:usd", 5.0, start.AddMinutes(10));

        Assert.Null(tracker.Estimate(
            "deepseek:usd", start.AddMinutes(10), configuredWindowMinutes: 60, providerWorking: true));

        tracker.Observe("deepseek:usd", 4.0, start.AddMinutes(15));
        var afterTopUp = tracker.Estimate(
            "deepseek:usd", start.AddMinutes(15), configuredWindowMinutes: 60, providerWorking: true);
        Assert.NotNull(afterTopUp);
        Assert.Equal(TimeSpan.FromMinutes(20), afterTopUp!.Remaining);
    }

    [Fact]
    public void ForecastWindowNeverClaimsFinerResolutionThanPolling()
    {
        var tracker = new DepletionForecastTracker();
        var start = new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);
        tracker.Observe("grok:weekly", 0.40, start);
        tracker.Observe("grok:weekly", 0.30, start.AddMinutes(15));

        var result = tracker.Estimate(
            "grok:weekly",
            start.AddMinutes(15),
            configuredWindowMinutes: 5,
            providerWorking: true,
            minimumWindowMinutes: 15);

        Assert.NotNull(result);
        Assert.Equal(15, result!.WindowMinutes);
    }
}

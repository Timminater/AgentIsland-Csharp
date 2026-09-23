using System.Reflection;
using AgentIsland.UI.Providers;
using AgentIsland.UI.Report;

namespace AgentIsland.Tests;

public class ReportProviderShareTests
{
    [WpfFact]
    public void TestReportUsesNeutralProviderShares() => RunAll();

    internal static void RunAll()
    {
        WpfTestEnvironment.EnsureInitialized();
        if (typeof(ReportCards).GetMethod("ResolveDuelSides", BindingFlags.Static | BindingFlags.NonPublic) is not null)
            throw new Exception("Report cards must not retain duel-side resolution.");

        var data = new WeeklyReportData(
            "Sep 16 – Sep 22",
            100,
            0,
            new[]
            {
                new ProviderPeriodSlice(DisplayProvider.Codex, 65),
                new ProviderPeriodSlice(DisplayProvider.Claude, 35),
            },
            new long[7],
            new[] { "M", "T", "W", "T", "F", "S", "S" },
            Array.Empty<ModelShare>());

        _ = ReportCards.Weekly(data);
        Console.WriteLine("PASS report cards render multi-provider usage without duel-side logic");
    }
}

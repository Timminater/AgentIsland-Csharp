namespace AgentIsland.UI;

/// Keeps the visible island inside a display while its transparent WPF canvas
/// is wider than the silhouette. Fractions survive scale and resolution changes.
internal static class TopBarPlacement
{
    internal static double WindowLeft(double areaLeft, double areaWidth,
        double canvasWidth, double silhouetteWidth, double fraction)
    {
        var inset = (canvasWidth - silhouetteWidth) / 2;
        var travel = Math.Max(0, areaWidth - silhouetteWidth);
        return areaLeft + travel * Math.Clamp(fraction, 0, 1) - inset;
    }

    internal static double ClampWindowLeft(double proposedLeft, double areaLeft,
        double areaWidth, double canvasWidth, double silhouetteWidth)
    {
        var min = WindowLeft(areaLeft, areaWidth, canvasWidth, silhouetteWidth, 0);
        var max = WindowLeft(areaLeft, areaWidth, canvasWidth, silhouetteWidth, 1);
        return Math.Clamp(proposedLeft, min, max);
    }

    internal static double Fraction(double windowLeft, double areaLeft,
        double areaWidth, double canvasWidth, double silhouetteWidth)
    {
        var travel = Math.Max(0, areaWidth - silhouetteWidth);
        if (travel == 0) return 0.5;
        var inset = (canvasWidth - silhouetteWidth) / 2;
        return Math.Clamp((windowLeft + inset - areaLeft) / travel, 0, 1);
    }
}

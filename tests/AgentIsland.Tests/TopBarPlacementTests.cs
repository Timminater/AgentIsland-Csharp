using AgentIsland.UI;

namespace AgentIsland.Tests;

[Collection("SettingsDiskTests")]
public sealed class TopBarPlacementTests
{
    [Theory]
    [InlineData(0, -210)]
    [InlineData(0.5, 250)]
    [InlineData(1, 710)]
    public void VisibleSilhouetteCanReachBothEdgesAndCenter(double fraction, double expectedLeft)
    {
        // Screen 100..1300, 900-DIP transparent canvas, 280-DIP island.
        Assert.Equal(expectedLeft,
            TopBarPlacement.WindowLeft(100, 1200, 900, 280, fraction), 4);
        Assert.Equal(fraction,
            TopBarPlacement.Fraction(expectedLeft, 100, 1200, 900, 280), 4);
    }

    [Fact]
    public void DragClampsToScreenAndExpansionKeepsPanelVisible()
    {
        Assert.Equal(-210, TopBarPlacement.ClampWindowLeft(-500, 100, 1200, 900, 280));
        Assert.Equal(710, TopBarPlacement.ClampWindowLeft(900, 100, 1200, 900, 280));

        var expandedLeft = TopBarPlacement.WindowLeft(100, 1200, 900, 700, 1);
        var inset = (900 - 700) / 2.0;
        Assert.Equal(1300, expandedLeft + inset + 700);

        // At 150% interface scale both the canvas and silhouette grow.
        var scaledLeft = TopBarPlacement.WindowLeft(0, 1920, 1350, 420, 1);
        Assert.Equal(1920, scaledLeft + (1350 - 420) / 2.0 + 420);
    }

    [Fact]
    public void TooNarrowScreenKeepsIslandAtAvailableLeftEdge()
    {
        Assert.Equal(-210, TopBarPlacement.WindowLeft(100, 200, 900, 280, 1));
        Assert.Equal(0.5, TopBarPlacement.Fraction(-210, 100, 200, 900, 280));
    }

    [Fact]
    public void HorizontalChoiceSurvivesRecreatingPositionStore()
    {
        var previous = AgentIsland.Windows.Preferences.Storage;
        AgentIsland.Windows.Preferences.Storage = new AgentIsland.Core.Storage.MemorySettingsStorage();
        try
        {
            var first = new IslandPositionStore();
            Assert.Equal(0.5, first.TopBarFraction);
            first.SetTopBarFraction(0.85);
            Assert.Equal(0.85, new IslandPositionStore().TopBarFraction);
        }
        finally
        {
            AgentIsland.Windows.Preferences.Storage = previous;
        }
    }
}

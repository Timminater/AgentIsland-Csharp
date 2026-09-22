using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AgentIsland.Providers.Usage.DeepSeek;
using AgentIsland.UI;

namespace AgentIsland.Tests;

/// Contract tests for DeepSeek's published peak / off-peak billing schedule.
///
/// The official price note is: "Peak hours are Beijing time Monday to Friday
/// 09:00-12:00 and 14:00-18:00; every other time is off-peak at half price."
/// These tests pin that sentence down as code — including the weekend rule
/// (which a naive "hour in [9,12) or [14,18)" check gets wrong) and the fact
/// that the bar renders the schedule on the user's own clock.
public class DeepSeekPeakHoursTests
{
    private static readonly TimeSpan Beijing = TimeSpan.FromHours(8);

    /// A Beijing-time instant, so the cases below read like the price list.
    private static DateTimeOffset BeijingTime(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, Beijing);

    [WpfFact]
    public void TestDeepSeekPeakHours() => RunAll();

    internal static void RunAll()
    {
        // These cases pin the ENGLISH copy, and L10n.Current=Auto follows the
        // machine's UI culture (a Dutch host would render "piek").
        var originalLanguage = AgentIsland.UI.Localization.L10n.Current;
        AgentIsland.UI.Localization.L10n.Current = AgentIsland.UI.Localization.L10n.Language.English;
        try
        {
            TestWeekdayPeakWindows();
            TestWindowEdges();
            TestWeekendIsAlwaysOffPeak();
            TestZoneIndependence();
            TestNextTransition();
            TestDayBandsAndShares();
            TestLocalRendering();
            TestCountdownFormatting();
            TestPhaseCaptions();
            TestBarLayoutAndRender();
        }
        finally
        {
            // Leave the machine's real clock in place for every other test.
            DeepSeekPeakHours.ResetLocalOffset();
            AgentIsland.UI.Localization.L10n.Current = originalLanguage;
        }
        Console.WriteLine("DeepSeekPeakHoursTests GREEN");
    }

    private static void TestWeekdayPeakWindows()
    {
        // 2026-09-09 is a Wednesday.
        Expect(DeepSeekPeakHours.IsPeak(BeijingTime(2026, 9, 9, 9, 30)), "09:30 Beijing on a weekday is peak");
        Expect(DeepSeekPeakHours.IsPeak(BeijingTime(2026, 9, 9, 14, 15)), "14:15 Beijing on a weekday is peak");
        Expect(DeepSeekPeakHours.IsOffPeak(BeijingTime(2026, 9, 9, 13, 0)),
            "the 12:00-14:00 Beijing gap is off-peak");
        Expect(DeepSeekPeakHours.IsOffPeak(BeijingTime(2026, 9, 9, 19, 0)), "evenings are off-peak");
        Expect(DeepSeekPeakHours.IsOffPeak(BeijingTime(2026, 9, 9, 3, 0)), "nights are off-peak");
        Console.WriteLine("PASS DeepSeek weekday peak windows");
    }

    private static void TestWindowEdges()
    {
        // Windows are half-open [start, end): 12:00 already bills off-peak.
        Expect(DeepSeekPeakHours.IsOffPeak(BeijingTime(2026, 9, 9, 8, 59)), "08:59 is off-peak");
        Expect(DeepSeekPeakHours.IsPeak(BeijingTime(2026, 9, 9, 9, 0)), "09:00 opens the morning peak");
        Expect(DeepSeekPeakHours.IsOffPeak(BeijingTime(2026, 9, 9, 12, 0)), "12:00 closes the morning peak");
        Expect(DeepSeekPeakHours.IsPeak(BeijingTime(2026, 9, 9, 14, 0)), "14:00 opens the afternoon peak");
        Expect(DeepSeekPeakHours.IsOffPeak(BeijingTime(2026, 9, 9, 18, 0)), "18:00 closes the afternoon peak");
        Console.WriteLine("PASS DeepSeek peak window edges are half-open");
    }

    private static void TestWeekendIsAlwaysOffPeak()
    {
        // 2026-09-12 Saturday, 2026-09-13 Sunday.
        foreach (var hour in new[] { 9, 10, 11, 14, 15, 17 })
        {
            Expect(DeepSeekPeakHours.IsOffPeak(BeijingTime(2026, 9, 12, hour, 30)),
                $"Saturday {hour:00}:30 Beijing must be off-peak");
            Expect(DeepSeekPeakHours.IsOffPeak(BeijingTime(2026, 9, 13, hour, 30)),
                $"Sunday {hour:00}:30 Beijing must be off-peak");
        }
        Expect(DeepSeekPeakHours.InBeijing(BeijingTime(2026, 9, 12, 9, 0)).DayOfWeek == DayOfWeek.Saturday,
            "Saturday is a weekend day");
        Expect(DeepSeekPeakHours.InBeijing(BeijingTime(2026, 9, 11, 9, 0)).DayOfWeek == DayOfWeek.Friday,
            "Friday is a weekday");
        Console.WriteLine("PASS DeepSeek weekends carry no peak window");
    }

    private static void TestZoneIndependence()
    {
        // 01:30 UTC == 09:30 Beijing, so the same instant is peak whatever
        // offset the machine reports it in.
        var utc = new DateTimeOffset(2026, 9, 9, 1, 30, 0, TimeSpan.Zero);
        Expect(DeepSeekPeakHours.IsPeak(utc), "01:30 UTC is 09:30 Beijing — peak");
        Expect(DeepSeekPeakHours.IsPeak(utc.ToOffset(TimeSpan.FromHours(-7))),
            "the same instant in any local offset must evaluate identically");
        Expect(DeepSeekPeakHours.InBeijing(utc).Hour == 9, "01:30 UTC maps to 09:30 Beijing");
        Console.WriteLine("PASS DeepSeek schedule is evaluated in Beijing time");
    }

    private static void TestNextTransition()
    {
        Expect(DeepSeekPeakHours.NextTransition(BeijingTime(2026, 9, 9, 7, 0)) == BeijingTime(2026, 9, 9, 9, 0),
            "pre-open off-peak ends at the 09:00 peak");
        Expect(DeepSeekPeakHours.NextTransition(BeijingTime(2026, 9, 9, 10, 0)) == BeijingTime(2026, 9, 9, 12, 0),
            "morning peak ends at 12:00");
        Expect(DeepSeekPeakHours.NextTransition(BeijingTime(2026, 9, 9, 13, 0)) == BeijingTime(2026, 9, 9, 14, 0),
            $"midday gap ends at 14:00 (got {DeepSeekPeakHours.NextTransition(BeijingTime(2026, 9, 9, 13, 0)):O})");
        Expect(DeepSeekPeakHours.NextTransition(BeijingTime(2026, 9, 9, 15, 0)) == BeijingTime(2026, 9, 9, 18, 0),
            "afternoon peak ends at 18:00");
        Expect(DeepSeekPeakHours.NextTransition(BeijingTime(2026, 9, 9, 20, 0)) == BeijingTime(2026, 9, 10, 9, 0),
            "the evening rolls into the next day's 09:00 peak");
        Expect(DeepSeekPeakHours.NextTransition(BeijingTime(2026, 9, 11, 19, 0)) == BeijingTime(2026, 9, 14, 9, 0),
            "Friday evening skips the weekend and lands on Monday 09:00");
        Expect(DeepSeekPeakHours.NextTransition(BeijingTime(2026, 9, 12, 12, 0)) == BeijingTime(2026, 9, 14, 9, 0),
            "a Saturday phase ends Monday 09:00");
        Expect(DeepSeekPeakHours.NextTransition(BeijingTime(2026, 9, 13, 23, 0)) == BeijingTime(2026, 9, 14, 9, 0),
            "a Sunday night phase ends Monday 09:00");
        Expect(DeepSeekPeakHours.TimeUntilTransition(BeijingTime(2026, 9, 9, 11, 30)).TotalMinutes == 30,
            "the countdown is the distance to the transition");
        Console.WriteLine("PASS DeepSeek phase transitions and countdowns");
    }

    private static void TestDayBandsAndShares()
    {
        DeepSeekPeakHours.LocalOffset = Beijing;
        Expect(DeepSeekPeakHours.LocalOffset == Beijing,
            $"the test must pin the rendered offset (got {DeepSeekPeakHours.LocalOffset})");
        try
        {
            var weekday = DeepSeekPeakHours.DayBands(BeijingTime(2026, 9, 9, 12, 0));
            Expect(weekday.Count == 24, "the day bar carries 24 hourly bands");
            Expect(!weekday[8].IsPeak && weekday[9].IsPeak && weekday[11].IsPeak && !weekday[12].IsPeak,
                "the morning peak band spans 09:00-12:00 "
                + $"(offset {DeepSeekPeakHours.LocalOffset}, "
                + $"local {DeepSeekPeakHours.ToLocal(BeijingTime(2026, 9, 9, 12, 0)):O}, "
                + $"bands {string.Join(",", weekday.Where(band => band.IsPeak).Select(band => band.Hour))})");
            Expect(!weekday[13].IsPeak && weekday[14].IsPeak && weekday[17].IsPeak && !weekday[18].IsPeak,
                "the afternoon peak band spans 14:00-18:00");
            Expect(weekday.Count(band => band.IsPeak) == 7, "a weekday has 7 peak hours");
            Expect(Math.Abs(DeepSeekPeakHours.OffPeakShare(BeijingTime(2026, 9, 9, 12, 0)) - 17 / 24.0) < 1e-9,
                "a weekday is off-peak 17 of 24 hours");

            var saturday = DeepSeekPeakHours.DayBands(BeijingTime(2026, 9, 12, 12, 0));
            Expect(saturday.All(band => !band.IsPeak), "a weekend day bar is entirely off-peak");
            Expect(DeepSeekPeakHours.OffPeakShare(BeijingTime(2026, 9, 12, 12, 0)) == 1.0,
                "a weekend is 100% off-peak");
            Expect(DeepSeekPeakHours.PeakWindowsForDay(BeijingTime(2026, 9, 12, 12, 0)).Count == 0,
                "a weekend day has no peak windows");
        }
        finally
        {
            DeepSeekPeakHours.LocalOffset = Beijing;
        }
        Console.WriteLine("PASS DeepSeek day bands and off-peak shares");
    }

    /// The bar renders on the user's clock: the same Beijing schedule has to
    /// come back as local hour ranges, and the bands must follow.
    private static void TestLocalRendering()
    {
        // 09:00-12:00 and 14:00-18:00 Beijing == 01:00-04:00 and 06:00-10:00 UTC.
        DeepSeekPeakHours.LocalOffset = TimeSpan.Zero;
        Expect(DeepSeekPeakHours.LocalOffset == TimeSpan.Zero,
            $"the test must pin UTC (got {DeepSeekPeakHours.LocalOffset})");
        try
        {
            var windows = DeepSeekPeakHours.PeakWindowsForDay(BeijingTime(2026, 9, 9, 12, 0));
            Expect(windows.Count == 2, $"a UTC day shows both Beijing windows (got {windows.Count})");
            Expect(Math.Abs(windows[0].Start - 1) < 1e-6 && Math.Abs(windows[0].End - 4) < 1e-6,
                "the morning peak reads 01:00-04:00 UTC (got "
                + string.Join(", ", windows.Select(window => $"{window.Start:0.##}-{window.End:0.##}"))
                + $"; offset now {DeepSeekPeakHours.LocalOffset}; "
                + $"local of 09:00 Beijing {DeepSeekPeakHours.ToLocal(BeijingTime(2026, 9, 9, 9, 0)):O})");
            Expect(Math.Abs(windows[1].Start - 6) < 1e-6 && Math.Abs(windows[1].End - 10) < 1e-6,
                "the afternoon peak reads 06:00-10:00 UTC");

            var bands = DeepSeekPeakHours.DayBands(BeijingTime(2026, 9, 9, 12, 0));
            Expect(!bands[0].IsPeak && bands[1].IsPeak && bands[3].IsPeak && !bands[4].IsPeak,
                "UTC bands peak 01:00-04:00");
            Expect(!bands[5].IsPeak && bands[6].IsPeak && bands[9].IsPeak && !bands[10].IsPeak,
                "UTC bands peak 06:00-10:00");

            var transition = DeepSeekPeakHours.ToLocal(
                DeepSeekPeakHours.NextTransition(BeijingTime(2026, 9, 9, 10, 0)));
            Expect(transition.Hour == 4 && transition.Minute == 0,
                "the 12:00 Beijing close reads 04:00 UTC");
            Expect(DeepSeekPeakHours.LocalZoneLabel == "UTC+0", "the zone label names the rendered offset");
        }
        finally
        {
            DeepSeekPeakHours.LocalOffset = Beijing;
        }

        // A far-west zone shifts the windows into the previous local evening
        // and can split one Beijing window across the local midnight.
        // 2026-09-09 12:00 Beijing is 2026-09-08 21:00 in UTC-7, so this query
        // is about the LOCAL day of the 8th.
        DeepSeekPeakHours.LocalOffset = TimeSpan.FromHours(-7);
        try
        {
            var windows = DeepSeekPeakHours.PeakWindowsForDay(BeijingTime(2026, 9, 9, 12, 0));
            Expect(windows.Count == 3,
                $"a UTC-7 day wraps three Beijing windows (got {windows.Count}: "
                + string.Join(", ", windows.Select(window => $"{window.Start:0.##}-{window.End:0.##}"))
                + $"; local day {DeepSeekPeakHours.ToLocal(BeijingTime(2026, 9, 9, 12, 0)).Date:yyyy-MM-dd})");
            Expect(Math.Abs(windows[0].Start - 0) < 1e-6 && Math.Abs(windows[0].End - 3) < 1e-6,
                "the 8th's Beijing afternoon window runs to 03:00 local");
            Expect(Math.Abs(windows[1].Start - 18) < 1e-6 && Math.Abs(windows[1].End - 21) < 1e-6,
                "the 9th's morning peak reads 18:00-21:00 on the previous local evening");
            Expect(Math.Abs(windows[2].Start - 23) < 1e-6 && Math.Abs(windows[2].End - 24) < 1e-6,
                "the 9th's afternoon peak starts 23:00 local and crosses midnight");

            var bands = DeepSeekPeakHours.DayBands(BeijingTime(2026, 9, 9, 12, 0));
            Expect(bands[0].IsPeak && bands[1].IsPeak && bands[2].IsPeak && !bands[3].IsPeak,
                "UTC-7 bands peak 00:00-03:00 from the 8th's Beijing afternoon window");
            Expect(bands[18].IsPeak && bands[20].IsPeak && !bands[17].IsPeak && !bands[21].IsPeak,
                "UTC-7 bands peak 18:00-21:00");
            Expect(bands[23].IsPeak, "UTC-7 bands peak from 23:00 to midnight");
            Expect(DeepSeekPeakHours.LocalZoneLabel == "UTC-7", "the zone label names a negative offset");
        }
        finally
        {
            DeepSeekPeakHours.LocalOffset = Beijing;
        }
        Console.WriteLine("PASS DeepSeek schedule converts to the rendered zone");
    }

    private static void TestCountdownFormatting()
    {
        Expect(DeepSeekPeakHoursBar.FormatCountdown(TimeSpan.FromHours(5)) == "5h", "whole hours read 5h");
        Expect(DeepSeekPeakHoursBar.FormatCountdown(TimeSpan.FromMinutes(95)) == "1h 35m",
            "hours and minutes read 1h 35m");
        Expect(DeepSeekPeakHoursBar.FormatCountdown(TimeSpan.FromMinutes(20)) == "20m",
            "under an hour reads minutes");
        Expect(DeepSeekPeakHoursBar.FormatCountdown(TimeSpan.FromSeconds(-5)) == "1m",
            "a passed transition never renders a negative countdown");
        Console.WriteLine("PASS DeepSeek countdown formatting");
    }

    private static void TestPhaseCaptions()
    {
        var peak = DeepSeekPeakHoursText.Tag(BeijingTime(2026, 9, 9, 10, 0));
        var off = DeepSeekPeakHoursText.Tag(BeijingTime(2026, 9, 9, 20, 0));
        Expect(peak == "peak", "the peak tag reads 'peak' in English");
        Expect(off == "off-peak", "the off-peak tag reads 'off-peak' in English");
        Expect(DeepSeekPeakHoursText.Short(BeijingTime(2026, 9, 9, 20, 0)).Contains("50% off"),
            "the off-peak caption states the discount");
        Expect(DeepSeekPeakHoursText.Long(BeijingTime(2026, 9, 9, 10, 0)).Contains("→"),
            "the caption names the closing time");
        Expect(DeepSeekPeakHoursText.ScheduleTooltip().Contains("Beijing"),
            "the tooltip names the pricing zone");
        Expect(DeepSeekPeakHoursText.Clock(9.5) == "09:30", "fractional hours render as HH:mm");
        Console.WriteLine("PASS DeepSeek phase captions");
    }

    /// The bar must fit the DeepSeek usage column (96 DIP of the 188-DIP
    /// panel) at a glanceable height, and paint the right pixels — a band or
    /// the marker laid out past the strip would come out blank. The rendered
    /// PNG is written beside the test output for eyeballing, and its pixels
    /// are asserted so the check is not just "a file exists".
    private static void TestBarLayoutAndRender()
    {
        var bar = new DeepSeekPeakHoursBar();
        bar.Measure(new Size(260, double.PositiveInfinity));
        Expect(bar.DesiredSize.Height > 0 && bar.DesiredSize.Height <= 40,
            $"the day bar must fit the usage column (got {bar.DesiredSize.Height:0.#} DIP)");
        bar.Arrange(new Rect(0, 0, 260, bar.DesiredSize.Height));
        bar.UpdateLayout();
        Expect(bar.ActualWidth == 260, "the bar must fill the column width");

        // The now-marker must land on the current local time across the strip.
        var local = DeepSeekPeakHours.ToLocal(DateTimeOffset.Now);
        var fraction = (local.Hour * 3600.0 + local.Minute * 60.0 + local.Second) / 86400.0;
        var expected = 260 * fraction - 0.75;
        Expect(Math.Abs(bar.MarkerMargin.Left - expected) < 2,
            $"the now-marker must sit at the current local time "
            + $"({local:HH:mm}, expected {expected:0.##}, got {bar.MarkerMargin.Left:0.##})");
        // The label is a 30-DIP block CENTRED on the marker (left = x - 15),
        // clamped inside the 260-DIP strip — not offset by the marker's own
        // half-width, which is only 0.75.
        var labelExpected = Math.Clamp(260 * fraction - 15, 0, 230);
        Expect(Math.Abs(bar.TimeLabelLeft - labelExpected) < 2,
            $"the current-time label must ride the marker "
            + $"(expected {labelExpected:0.##}, got {bar.TimeLabelLeft:0.##})");

        const int scale = 2;
        var width = (int)Math.Ceiling(bar.ActualWidth * scale);
        var height = (int)Math.Ceiling(bar.DesiredSize.Height * scale);
        // RenderTargetBitmap.Render on a window-less element can drop brushes
        // that were never composed; drawing a VisualBrush of the laid-out
        // element into a DrawingVisual (the app's own snapshot recipe) keeps
        // every band and the marker.
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
            context.DrawRectangle(
                new VisualBrush(bar) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                null,
                new Rect(0, 0, bar.ActualWidth, bar.DesiredSize.Height));
        }
        var bitmap = new RenderTargetBitmap(
            width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var directory = Path.Combine(AppContext.BaseDirectory, "peak-hours-preview");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "deepseek-peak-bar.png");
        using (var stream = File.Create(path))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
        }
        Expect(new FileInfo(path).Length > 0, "the day bar must render to a non-empty PNG");

        // Count the two band tints plus the red now-marker. They are the
        // fills the control itself paints, so this pins the colour split.
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        var offPeak = 0;
        var peak = 0;
        var now = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var blue = pixels[i];
            var green = pixels[i + 1];
            var red = pixels[i + 2];
            if (pixels[i + 3] < 200) continue;
            if (red > 200 && green < 130 && blue < 130) now++;
            else if (green > red + 20 && green > blue) offPeak++;
            else if (red > blue + 20 && green > blue) peak++;
        }
        Expect(offPeak > 0, "the day bar must paint off-peak bands");
        Expect(now > 0, "the day bar must paint the red now-marker");
        Expect(DeepSeekPeakHours.OffPeakShare(DateTimeOffset.Now) < 1.0 ? peak > 0 : peak == 0,
            "peak bands appear on a weekday bar and never on a weekend bar");

        // The whole balance face — hero, captions, and this bar — has to fit
        // the DeepSeek usage column, which shares the 96-DIP slot with the
        // percentage columns. The bar is the part that grew, so pin its budget.
        var block = new DeepSeekBalanceBlock();
        block.Measure(new Size(260, double.PositiveInfinity));
        Expect(block.DesiredSize.Height <= DeepSeekBalanceBlock.ProviderChartsBlockHeight,
            $"the DeepSeek column must fit its {DeepSeekBalanceBlock.ProviderChartsBlockHeight:0} DIP slot "
            + $"(got {block.DesiredSize.Height:0.#} DIP)");

        Console.WriteLine(
            $"PASS DeepSeek day bar layout + render ({bar.DesiredSize.Height:0.#} DIP, "
            + $"off-peak {offPeak}px, peak {peak}px, marker {now}px) -> {path}");
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

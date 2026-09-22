using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AgentIsland.Providers.Usage.DeepSeek;
using AgentIsland.UI.Charts;
using AgentIsland.UI.Localization;
using AgentIsland.UI.Theme;

namespace AgentIsland.UI;

/// <summary>
/// A 24-hour DeepSeek billing bar: one column per hour of the user's own day,
/// off-peak hours tinted teal and peak hours amber, with a red now-marker —
/// a thin line through the strip, a pointer above it, and the current time
/// printed over the pointer.
///
/// The published windows are Beijing time, so the bands are derived from the
/// Beijing schedule and then rendered on the local clock (DeepSeekPeakHours
/// does the conversion). Weekends carry no peak window at all, so those days
/// render as one solid off-peak band and say so.
/// </summary>
public sealed class DeepSeekPeakHoursBar : Grid
{
    /// DeepSeek's off-peak tint — the app's live-teal, dimmed to a fill.
    private static readonly Color OffPeakFill = Color.FromRgb(0x1F, 0x7A, 0x66);

    /// Peak tint — the app's alert amber, dimmed to a fill.
    private static readonly Color PeakFill = Color.FromRgb(0x8A, 0x5A, 0x14);

    /// The now-marker: the app's alert red, bright enough to read on both
    /// band tints at 1.5 DIP.
    private static readonly Color NowTint = Color.FromRgb(0xFF, 0x4D, 0x52);

    private const double BandHeight = 9;
    private const double MarkerWidth = 1.5;
    private const double PointerWidth = 7;
    private const double PointerHeight = 5;
    private const double TimeLabelWidth = 30;

    private readonly Grid _bands = new() { Height = BandHeight };
    private readonly Grid _hourLabels = new() { Height = BandHeight, IsHitTestVisible = false };
    private readonly Canvas _pointerRow = new() { Height = 11 };
    private readonly Border _marker;
    private readonly Polygon _pointer;
    private readonly TextBlock _timeLabel;
    private readonly TextBlock _status;
    private readonly TextBlock _zone;
    private readonly DispatcherTimer _tick;

    /// The rendered day the bands were painted for; a day rollover repaints,
    /// a same-day tick only moves the marker.
    private DateTime _paintedDay = DateTime.MinValue;

    public DeepSeekPeakHoursBar()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // status line
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // pointer + time
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // band strip

        var header = new Grid { Margin = new Thickness(0, 0, 0, 1) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _status = new TextBlock
        {
            FontFamily = IslandFonts.Ui,
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        header.Children.Add(_status);

        _zone = new TextBlock
        {
            FontFamily = IslandFonts.Mono,
            FontSize = 8,
            Foreground = IslandColors.Brush(IslandColors.White(0.35)),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
        };
        Grid.SetColumn(_zone, 1);
        header.Children.Add(_zone);
        SetRow(header, 0);
        Children.Add(header);

        // Pointer row: the red triangle sits on the marker's x, the current
        // time rides just above it. Both are clamped so the ends of the day
        // stay readable.
        _pointer = new Polygon
        {
            Points = new PointCollection
            {
                new Point(0, 0),
                new Point(PointerWidth, 0),
                new Point(PointerWidth / 2, PointerHeight),
            },
            Fill = IslandColors.Brush(NowTint),
            IsHitTestVisible = false,
        };
        _pointerRow.Children.Add(_pointer);

        _timeLabel = new TextBlock
        {
            FontFamily = IslandFonts.Mono,
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = IslandColors.Brush(NowTint),
            IsHitTestVisible = false,
        };
        _pointerRow.Children.Add(_timeLabel);
        SetRow(_pointerRow, 1);
        Children.Add(_pointerRow);

        // The band strip: 24 star columns so the bands scale with the panel.
        _bands.ClipToBounds = true;
        for (var hour = 0; hour < 24; hour++)
        {
            _bands.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star),
            });
        }
        _bands.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(2),
            Background = IslandColors.Brush(IslandColors.White(0.05)),
        });
        Grid.SetColumnSpan(_bands.Children[0], 24);

        // The marker lives inside the strip: a zero-width column would steal
        // space from the 24 star columns, so it rides in column 0 and a left
        // margin walks it across the full width.
        _marker = new Border
        {
            Width = MarkerWidth,
            Background = IslandColors.Brush(NowTint),
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        Grid.SetColumn(_marker, 0);
        _bands.Children.Add(_marker);

        for (var hour = 0; hour < 24; hour++)
        {
            _hourLabels.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star),
            });
        }
        foreach (var hour in new[] { 0, 6, 12, 18 })
        {
            // The 00:00 and 18:00 labels sit on the strip's own corners: the
            // first hugs the left edge, the last stretches over the closing
            // six columns so its text lands flush right instead of being
            // clipped by a single star column.
            var label = new TextBlock
            {
                Text = $"{hour:00}",
                FontFamily = IslandFonts.Mono,
                FontSize = 8,
                Foreground = IslandColors.Brush(IslandColors.White(0.45)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = hour switch
                {
                    0 => HorizontalAlignment.Left,
                    18 => HorizontalAlignment.Right,
                    _ => HorizontalAlignment.Center,
                },
                Margin = hour switch
                {
                    0 => new Thickness(3, 0, 0, 0),
                    18 => new Thickness(0, 0, 3, 0),
                    _ => new Thickness(0),
                },
            };
            Grid.SetColumn(label, hour);
            Grid.SetColumnSpan(label, hour == 18 ? 6 : 1);
            _hourLabels.Children.Add(label);
        }

        var strip = new Grid();
        strip.Children.Add(_bands);
        strip.Children.Add(_hourLabels);
        SetRow(strip, 2);
        Children.Add(strip);

        // One repaint per minute is plenty: the marker moves ~0.07% of the
        // width per minute, and the phase caption only changes on the hour.
        _tick = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _tick.Tick += (_, _) => Refresh(DateTimeOffset.Now);
        Loaded += (_, _) => _tick.Start();
        Unloaded += (_, _) => _tick.Stop();

        Refresh(DateTimeOffset.Now);
    }

    /// Repaints the bands for the given instant and moves the now-marker.
    public void Refresh(DateTimeOffset now)
    {
        var local = DeepSeekPeakHours.ToLocal(now);
        if (local.Date != _paintedDay) PaintBands(now);
        MoveMarker(now);
        UpdateHeader(now);
    }

    /// The now-marker's current left offset, exposed for the render test.
    internal Thickness MarkerMargin => _marker.Margin;

    /// The current-time label's left offset, exposed for the render test.
    internal double TimeLabelLeft => Canvas.GetLeft(_timeLabel);

    private void PaintBands(DateTimeOffset now)
    {
        _paintedDay = DeepSeekPeakHours.ToLocal(now).Date;

        // Drop every band left from the previous day (the backing rect and the
        // marker stay put at indices 0 and 1).
        while (_bands.Children.Count > 2) _bands.Children.RemoveAt(2);

        foreach (var band in DeepSeekPeakHours.DayBands(now))
        {
            var block = new Border
            {
                Background = IslandColors.Brush(band.IsPeak ? PeakFill : OffPeakFill, 0.9),
                // A hairline gutter keeps the hours countable without a full
                // stroke around every one of them.
                BorderBrush = IslandColors.Brush(IslandColors.SilhouetteBlack, 0.6),
                BorderThickness = new Thickness(0, 0, 0.5, 0),
            };
            Grid.SetColumn(block, band.Hour);
            _bands.Children.Add(block);
        }
    }

    private void MoveMarker(DateTimeOffset now)
    {
        var width = _bands.ActualWidth;
        if (width <= 0) return;

        var local = DeepSeekPeakHours.ToLocal(now);
        var fraction = (local.Hour * 3600.0 + local.Minute * 60.0 + local.Second) / 86400.0;
        var x = Math.Clamp(width * fraction, 0, width);

        _marker.Margin = new Thickness(
            Math.Clamp(x - MarkerWidth / 2, 0, Math.Max(0, width - MarkerWidth)),
            0,
            0,
            0);
        Canvas.SetLeft(_pointer, Math.Clamp(x - PointerWidth / 2, 0, Math.Max(0, width - PointerWidth)));

        // The label stays inside the strip: left of the pointer mid-day, and
        // pulled back at either end so "23:59" never runs off the panel.
        _timeLabel.Text = local.ToString("HH:mm");
        var labelLeft = Math.Clamp(
            x - TimeLabelWidth / 2,
            0,
            Math.Max(0, width - TimeLabelWidth));
        Canvas.SetLeft(_timeLabel, labelLeft);
        Canvas.SetTop(_timeLabel, 0);
    }

    private void UpdateHeader(DateTimeOffset now)
    {
        var peak = DeepSeekPeakHours.IsPeak(now);
        var tint = peak ? IslandColors.AlertAmber : IslandColors.LiveTeal;

        _status.Inlines.Clear();
        _status.Inlines.Add(new Run(peak ? L10n.Tr("Peak hours") : L10n.Tr("Off-peak"))
        {
            Foreground = IslandColors.Brush(tint),
        });
        if (!peak)
        {
            _status.Inlines.Add(new Run(" · " + L10n.Tr("50% off"))
            {
                Foreground = IslandColors.Brush(IslandColors.White(0.6)),
            });
        }

        var transition = DeepSeekPeakHours.ToLocal(DeepSeekPeakHours.NextTransition(now));
        var when = DeepSeekPeakHours.TransitionIsAnotherDay(now)
            ? transition.ToString("ddd HH:mm")
            : transition.ToString("HH:mm");
        _status.Inlines.Add(new Run(
            " · " + L10n.TrFormat("{0} → {1}", FormatCountdown(DeepSeekPeakHours.TimeUntilTransition(now)), when))
        {
            Foreground = IslandColors.Brush(IslandColors.White(0.5)),
        });

        var zone = DeepSeekPeakHours.LocalZoneLabel;
        _zone.Text = DeepSeekPeakHours.IsWeekend(now)
            ? L10n.TrFormat("weekend · {0}", zone)
            : zone;
        ToolTip = DeepSeekPeakHoursText.ScheduleTooltip();
    }

    /// Compact phase countdown: hours while far out, minutes in the last hour.
    internal static string FormatCountdown(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        if (remaining.TotalHours >= 1)
        {
            var hours = (int)remaining.TotalHours;
            return remaining.Minutes == 0 ? $"{hours}h" : $"{hours}h {remaining.Minutes:00}m";
        }
        return $"{Math.Max(1, (int)Math.Round(remaining.TotalMinutes))}m";
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        MoveMarker(DateTimeOffset.Now);
        return size;
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AgentIsland.UI.Providers;
using AgentIsland.UI.Charts;
using AgentIsland.UI.Theme;

namespace AgentIsland.UI.Report;

/// The two share cards, v3 layout (locked 2026-07-16):
///   header  — app logo top-left on the wordmark line, date right
///   hero    — big number with the "≈ $X API value" line sharing its
///             baseline
///   providers — one neutral usage-share bar with a compact provider legend
///   middle  — weekly: 7-day bars (peak in brand teal + its value) then a
///             TOP-3 model pie; monthly: a TOP-5 model pie (heatmap gone)
///   footer  — rank block, no plate, no divider: lifetime line + the
///             congratulations line in gold
/// Flat #0D0F13 base, no gradients. Numbers render tabular (no slashed
/// zeros). Fixed 420x560 portrait; `rounded: false` builds the EXPORT
/// version — square corners and full-bleed, because social apps flatten
/// transparency to white and rounded transparent corners paste as nicks.
public static partial class ReportCards
{
    public const double CardWidth = 420;
    public const double CardHeight = 560;

    private static readonly Color Base = Color.FromRgb(0x0D, 0x0F, 0x13);
    private static readonly Color LiveTeal = Color.FromRgb(0x3D, 0xD6, 0x8C);
    private static readonly Color PeriodAccent = Color.FromRgb(0x2D, 0xD4, 0xBF); // Teal accent for WEEKLY/MONTHLY & peak bar
    private static readonly Color PriceGreen = Color.FromRgb(0x34, 0xD3, 0x99); // Emerald green for dollar amounts

    public static FrameworkElement Weekly(WeeklyReportData data, bool rounded = true)
    {
        var dutch = ReportFormat.IsDutch;
        var hasActualDollars = data.TotalDollars >= 1 && data.Providers.Any(p => p.Tokens > 0 && ReportFormat.ProvidesDollars(p.Provider));
        var hasUnpriced = data.Providers.Any(p => p.Tokens > 0 && !ReportFormat.ProvidesDollars(p.Provider));
        var isPartial = hasActualDollars && hasUnpriced;

        var body = BuildWeeklyLayout(
            Header("WEEKLY", data.RangeText),
            Hero(AgentIsland.UI.Localization.L10n.Tr("tokens this week"), data.TotalTokens, data.TotalDollars, hasActualDollars, isPartial, dutch),
            ProviderShareStage(data.Providers),
            WeekBars(data, dutch),
            ModelTable(data.TopModels, dutch, data.OmittedModelsCount, data.OmittedPercent));
        return Card(body, rounded);
    }

    public static FrameworkElement Monthly(MonthlyReportData data, bool rounded = true)
    {
        var dutch = ReportFormat.IsDutch;
        var hasActualDollars = data.TotalDollars >= 1 && data.Providers.Any(p => p.Tokens > 0 && ReportFormat.ProvidesDollars(p.Provider));
        var hasUnpriced = data.Providers.Any(p => p.Tokens > 0 && !ReportFormat.ProvidesDollars(p.Provider));
        var isPartial = hasActualDollars && hasUnpriced;

        var body = BuildMonthlyLayout(
            Header("MONTHLY", data.MonthText),
            Hero(AgentIsland.UI.Localization.L10n.Tr("tokens this month"), data.TotalTokens, data.TotalDollars, hasActualDollars, isPartial, dutch),
            ProviderShareStage(data.Providers),
            ModelTable(data.TopModels, dutch, data.OmittedModelsCount, data.OmittedPercent));
        return Card(body, rounded);
    }

    private static Grid BuildWeeklyLayout(
        UIElement header, UIElement hero, UIElement providerShares, UIElement weekBars, UIElement modelTable)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 10, MaxHeight = 16 });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 12, MaxHeight = 20 });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 12, MaxHeight = 20 });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 12, MaxHeight = 20 });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 0 });

        Grid.SetRow((FrameworkElement)header, 0);
        grid.Children.Add(header);
        Grid.SetRow((FrameworkElement)hero, 2);
        grid.Children.Add(hero);
        Grid.SetRow((FrameworkElement)providerShares, 4);
        grid.Children.Add(providerShares);
        Grid.SetRow((FrameworkElement)weekBars, 6);
        grid.Children.Add(weekBars);
        Grid.SetRow((FrameworkElement)modelTable, 8);
        grid.Children.Add(modelTable);

        return grid;
    }

    private static Grid BuildMonthlyLayout(
        UIElement header, UIElement hero, UIElement providerShares, UIElement modelTable)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 10, MaxHeight = 16 });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 14 });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 16 });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 0 });

        Grid.SetRow((FrameworkElement)header, 0);
        grid.Children.Add(header);
        Grid.SetRow((FrameworkElement)hero, 2);
        grid.Children.Add(hero);
        Grid.SetRow((FrameworkElement)providerShares, 4);
        grid.Children.Add(providerShares);
        Grid.SetRow((FrameworkElement)modelTable, 6);
        grid.Children.Add(modelTable);
        grid.RowDefinitions[^1].Height = new GridLength(0.5, GridUnitType.Star);

        return grid;
    }

    // MARK: - Scaffold

    private static FrameworkElement Card(UIElement body, bool rounded)
    {
        var radius = rounded ? 24.0 : 0.0;
        var root = new Grid { Width = CardWidth, Height = CardHeight, UseLayoutRounding = true, SnapsToDevicePixels = true };

        // v3: one flat base, no gradients, no auras. A uniform hairline
        // keeps the card from melting into dark chat backgrounds.
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(radius),
            Background = IslandColors.Brush(Base),
            BorderThickness = new Thickness(1),
            // Opaque equivalent of 16% white over Base prevents a white
            // fringe when the rounded PNG is viewed on a light background.
            BorderBrush = IslandColors.Brush(Color.FromRgb(0x34, 0x35, 0x39)),
        });

        var content = new Grid { Margin = new Thickness(28) };
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(body, 0);
        content.Children.Add(body);
        root.Children.Add(content);

        root.Clip = new RectangleGeometry(new Rect(0, 0, CardWidth, CardHeight), radius, radius);
        System.Windows.Media.TextOptions.SetTextFormattingMode(root, TextFormattingMode.Ideal);
        return root;
    }

    /// Numbers on the card read tabular — and Segoe UI's zero carries no
    /// slash, which the v3 spec calls out explicitly.
    private static TextBlock Numeric(TextBlock block)
    {
        Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular);
        return block;
    }

    /// WPF has no letter-spacing; interleaved spaces fake the macOS wide
    /// wordmark tracking (the mac card spaces its letters visibly apart).
    private static string Track(string text) => string.Join(' ', text.ToCharArray());

    private static UIElement Header(string tag, string rangeText)
    {
        var row = new DockPanel { LastChildFill = false };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        // v4: the BARE transparent five-blade mark — neither a plate nor
        // the app icon (owner review ×2, 2026-08-09). The small-optimized
        // variant keeps the blades separable at 22px.
        try
        {
            var logo = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/AgentIsland;component/Assets/agentisland_logo_small.png")),
                Width = 22,
                Height = 22,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 9, 0),
            };
            RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
            brand.Children.Add(logo);
        }
        catch
        {
        }
        brand.Children.Add(new TextBlock
        {
            Text = Track("AGENT ISLAND"),
            FontFamily = IslandFonts.Ui,
            FontSize = 11,
            FontWeight = FontWeights.ExtraBold,
            Foreground = IslandColors.Brush(IslandColors.White(0.88)),
            VerticalAlignment = VerticalAlignment.Center,
        });
        brand.Children.Add(new TextBlock
        {
            Text = " " + Track(AgentIsland.UI.Localization.L10n.Tr(tag)),
            FontFamily = IslandFonts.Ui,
            FontSize = 11,
            FontWeight = FontWeights.ExtraBold,
            Foreground = IslandColors.Brush(PeriodAccent),
            VerticalAlignment = VerticalAlignment.Center,
        });
        DockPanel.SetDock(brand, Dock.Left);
        row.Children.Add(brand);
        var range = Numeric(new TextBlock
        {
            Text = rangeText,
            FontFamily = IslandFonts.Ui,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = IslandColors.Brush(IslandColors.White(0.65)),
            VerticalAlignment = VerticalAlignment.Center,
        });
        DockPanel.SetDock(range, Dock.Right);
        row.Children.Add(range);
        return row;
    }

    private static UIElement Hero(
        string title, long totalTokens, double totalDollars,
        bool hasActualDollars, bool isPartialDollars, bool dutch)
    {
        var stack = new StackPanel();

        var titleRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 6) };
        var titleBlock = new TextBlock
        {
            Text = title,
            FontFamily = IslandFonts.Ui,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = IslandColors.Brush(IslandColors.White(0.50)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(titleBlock, Dock.Left);
        titleRow.Children.Add(titleBlock);

        stack.Children.Add(titleRow);

        var (value, unit) = ReportFormat.CompactParts(totalTokens, dutch);
        var line = new StackPanel { Orientation = Orientation.Horizontal };
        var numberBrush = IslandColors.Brush(Color.FromRgb(0xF2, 0xF5, 0xF7));
        line.Children.Add(Numeric(new TextBlock
        {
            Text = value,
            FontFamily = IslandFonts.Ui,
            FontSize = 50,
            FontWeight = FontWeights.ExtraBold,
            Foreground = numberBrush,
            LineHeight = 54,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
        }));
        if (unit.Length > 0)
        {
            line.Children.Add(new TextBlock
            {
                Text = unit,
                FontFamily = IslandFonts.Ui,
                FontSize = 50,
                FontWeight = FontWeights.ExtraBold,
                Foreground = numberBrush,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(3, 0, 0, 0),
            });
        }
        if (hasActualDollars)
        {
            var money = ReportFormat.Money(totalDollars);
            string dollarText;
            if (isPartialDollars)
            {
                dollarText = dutch
                    ? $"≈ ${money} aan API-kosten (gedeeltelijke schatting)"
                    : $"≈ ${money} API value (partial estimate)";
            }
            else
            {
                dollarText = dutch
                    ? $"≈ ${money} aan API-kosten"
                    : $"≈ ${money} API value";
            }

            line.Children.Add(Numeric(new TextBlock
            {
                Text = dollarText,
                FontFamily = IslandFonts.Ui,
                FontSize = 12.5,
                FontWeight = FontWeights.ExtraBold,
                Foreground = IslandColors.Brush(PriceGreen),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(12, 0, 0, 7),
            }));
        }
        stack.Children.Add(new Viewbox
        {
            Child = line,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            MaxWidth = CardWidth - 56,
            HorizontalAlignment = HorizontalAlignment.Left,
        });
        return stack;
    }

    // MARK: - Provider usage distribution

    private const double ShareBarHeight = 6;

    private static UIElement ProviderShareStage(IReadOnlyList<ProviderPeriodSlice> providers)
    {
        var contentWidth = CardWidth - 56; // 28pt card padding each side
        var stack = new StackPanel();
        var active = providers.Where(p => p.Tokens > 0).ToList();

        if (active.Count > 0)
        {
            RenderProviderShares(stack, active, contentWidth);
            return stack;
        }

        RenderEmptyStage(stack, contentWidth);
        return stack;
    }

    private static void RenderProviderShares(
        StackPanel stack,
        IReadOnlyList<ProviderPeriodSlice> providers,
        double contentWidth)
    {
        var total = (double)providers.Sum(provider => provider.Tokens);
        var bar = new Grid { Width = contentWidth, Height = ShareBarHeight, HorizontalAlignment = HorizontalAlignment.Left };
        bar.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            Background = IslandColors.Brush(IslandColors.White(0.08)),
        });

        var segments = new Grid { ClipToBounds = true };
        foreach (var provider in providers)
        {
            var share = total > 0 ? provider.Tokens / total : 0;
            segments.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(share, GridUnitType.Star) });
            var segment = new Border { Background = IslandColors.Brush(ProviderIdentity.Accent(provider.Provider)) };
            Grid.SetColumn(segment, segments.ColumnDefinitions.Count - 1);
            segments.Children.Add(segment);
        }
        bar.Children.Add(segments);
        stack.Children.Add(bar);

        var legend = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
        };
        foreach (var provider in providers)
        {
            var share = total > 0 ? provider.Tokens / total : 0;
            var item = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5, 2, 5, 2),
            };
            item.Children.Add(new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = IslandColors.Brush(ProviderIdentity.Accent(provider.Provider)),
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            item.Children.Add(Numeric(new TextBlock
            {
                Text = $"{ProviderIdentity.DisplayName(provider.Provider)} {Core.Formatting.PercentInt(share)}%",
                FontFamily = IslandFonts.Ui,
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = IslandColors.Brush(IslandColors.White(0.72)),
            }));
            legend.Children.Add(item);
        }
        stack.Children.Add(legend);
    }

    private static void RenderEmptyStage(StackPanel stack, double contentWidth)
    {
        stack.Children.Add(new TextBlock
        {
            Text = AgentIsland.UI.Localization.L10n.Tr("No activity recorded"),
            FontFamily = IslandFonts.Ui,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = IslandColors.Brush(IslandColors.White(0.35)),
            Width = contentWidth,
            TextAlignment = TextAlignment.Center,
        });
    }

    private static UIElement ProviderMark(DisplayProvider provider, double side = 18) =>
        ProviderMarks.Mark(provider, side, tintOpacity: 1);

    // MARK: - Weekly bars

    private static UIElement WeekBars(WeeklyReportData data, bool dutch)
    {
        var peak = data.DailyTokens.Count > 0 ? Math.Max(data.DailyTokens.Max(), 1) : 1;
        var hasAnyTokens = data.DailyTokens.Any(t => t > 0);
        var grid = new Grid();
        for (var i = 0; i < 7; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        for (var i = 0; i < 7; i++)
        {
            var tokens = i < data.DailyTokens.Count ? data.DailyTokens[i] : 0;
            var isPeak = tokens == peak && tokens > 0;
            var cell = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(4, 0, 4, 0),
            };
            // The value slot exists on every column (blank when not the
            // peak) so all seven bars share one floor line (macOS keeps the
            // row with a " " placeholder).
            cell.Children.Add(Numeric(new TextBlock
            {
                Text = isPeak ? ReportFormat.CompactString(tokens, dutch) : " ",
                FontFamily = IslandFonts.Ui,
                FontSize = 9.5,
                FontWeight = FontWeights.ExtraBold,
                Foreground = IslandColors.Brush(isPeak ? PeriodAccent : Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5),
            }));
            var barHeight = hasAnyTokens ? Math.Max(5, 54.0 * tokens / peak) : 5.0;
            cell.Children.Add(new Border
            {
                Height = barHeight,
                CornerRadius = new CornerRadius(4),
                Background = isPeak
                    ? IslandColors.Brush(PeriodAccent)
                    : IslandColors.Brush(tokens > 0 ? IslandColors.White(0.16) : IslandColors.White(0.07)),
            });
            cell.Children.Add(new TextBlock
            {
                Text = i < data.DayLetters.Count ? data.DayLetters[i] : "",
                FontFamily = IslandFonts.Ui,
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = IslandColors.Brush(isPeak ? PeriodAccent : IslandColors.White(0.35)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0),
            });
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }
        return grid;
    }

    // MARK: - Model donut + rows

    /// Donut with "MODELS" in the hole and plain rows beside it. Segments sweep
    /// the TRUE share of tokens; the uncovered arc IS the long tail, so nothing
    /// is normalized to 100%. Long model names truncate cleanly without crushing
    /// numbers or percent columns.
    private static UIElement ModelTable(
        IReadOnlyList<ModelShare> models, bool dutch, int omittedCount = 0, double omittedPercent = 0)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        const double size = 88;
        const double thickness = 12;
        var donut = new Grid { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
        donut.Children.Add(new Ellipse
        {
            Stroke = IslandColors.Brush(IslandColors.White(0.08)),
            StrokeThickness = thickness,
            Margin = new Thickness(thickness / 2),
        });
        var cumulative = 0.0;
        foreach (var model in models)
        {
            var from = cumulative;
            cumulative += model.Percent;
            var gap = model.Percent > 0.03 ? 0.006 : 0.0;
            var start = from + gap;
            var end = Math.Max(start, cumulative - gap);
            if (end - start <= 0.0005) continue;
            donut.Children.Add(DonutSegment(size, thickness, start, end, model.Color));
        }
        donut.Children.Add(new TextBlock
        {
            Text = AgentIsland.UI.Localization.L10n.Tr("MODELS"),
            FontFamily = IslandFonts.Ui,
            FontSize = 10.5,
            FontWeight = FontWeights.ExtraBold,
            Foreground = IslandColors.Brush(IslandColors.White(0.48)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(donut, 0);
        row.Children.Add(donut);

        var rightStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        if (models.Count == 0)
        {
            var emptyText = new TextBlock
            {
                Text = AgentIsland.UI.Localization.L10n.Tr("No model activity"),
                FontFamily = IslandFonts.Ui,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = IslandColors.Brush(IslandColors.White(0.35)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10),
            };
            rightStack.Children.Add(emptyText);
        }
        else
        {
            var table = new Grid();
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });

            var pad = models.Count > 3 ? 3.0 : 4.5;
            var rowIndex = 0;
            foreach (var model in models)
            {
                table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var namePanel = new DockPanel
                {
                    LastChildFill = true,
                    Margin = new Thickness(0, pad, 0, pad),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var dot = new Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Fill = IslandColors.Brush(model.Color),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0),
                };
                DockPanel.SetDock(dot, Dock.Left);
                namePanel.Children.Add(dot);

                var nameText = new TextBlock
                {
                    Text = ReportFormat.DisplayModelName(model.Name),
                    FontFamily = IslandFonts.Ui,
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = IslandColors.Brush(IslandColors.White(0.88)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                namePanel.Children.Add(nameText);
                Grid.SetRow(namePanel, rowIndex);
                Grid.SetColumn(namePanel, 0);
                table.Children.Add(namePanel);

                TextBlock Cell(string text, Color color, TextAlignment align)
                {
                    return Numeric(new TextBlock
                    {
                        Text = text,
                        FontFamily = IslandFonts.Ui,
                        FontSize = 10.5,
                        FontWeight = FontWeights.ExtraBold,
                        Foreground = IslandColors.Brush(color),
                        TextAlignment = align,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(6, pad, 0, pad),
                    });
                }

                var tokens = Cell(ReportFormat.CompactString(model.Tokens, dutch), IslandColors.White(0.55), TextAlignment.Right);
                Grid.SetRow(tokens, rowIndex);
                Grid.SetColumn(tokens, 1);
                table.Children.Add(tokens);

                var dollars = Cell(
                    ReportFormat.ProvidesDollars(model.Provider)
                        ? $"${ReportFormat.Money(model.Dollars)}"
                        : "—",
                    ReportFormat.ProvidesDollars(model.Provider)
                        ? Color.FromRgb(0x8C, 0xD9, 0x9E)
                        : IslandColors.White(0.28),
                    TextAlignment.Right);
                Grid.SetRow(dollars, rowIndex);
                Grid.SetColumn(dollars, 2);
                table.Children.Add(dollars);

                var share = Cell($"{Core.Formatting.PercentInt(model.Percent)}%", IslandColors.White(0.90), TextAlignment.Right);
                Grid.SetRow(share, rowIndex);
                Grid.SetColumn(share, 3);
                table.Children.Add(share);
                rowIndex++;
            }
            rightStack.Children.Add(table);

            if (omittedCount > 0 && omittedPercent > 0)
            {
                var percentText = omittedPercent < 0.005 ? "<1" : Core.Formatting.PercentInt(omittedPercent).ToString();
                var omittedText = dutch
                    ? $"Overige {omittedCount} modellen ({percentText}%)"
                    : $"{omittedCount} other models ({percentText}%)";
                var omittedBlock = new TextBlock
                {
                    Text = omittedText,
                    FontFamily = IslandFonts.Ui,
                    FontSize = 9.5,
                    FontWeight = FontWeights.Medium,
                    Foreground = IslandColors.Brush(IslandColors.White(0.40)),
                    Margin = new Thickness(13, 4, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                rightStack.Children.Add(omittedBlock);
            }
        }

        Grid.SetColumn(rightStack, 2);
        row.Children.Add(rightStack);
        return row;
    }

    public static RenderTargetBitmap RenderToBitmap(FrameworkElement card, double scale = 3)
    {
        card.Measure(new Size(CardWidth, CardHeight));
        card.Arrange(new Rect(0, 0, CardWidth, CardHeight));
        card.UpdateLayout();
        var bitmap = new RenderTargetBitmap(
            (int)(CardWidth * scale), (int)(CardHeight * scale),
            96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(card);
        bitmap.Freeze();
        return bitmap;
    }

    public static void SavePng(FrameworkElement card, string path, double scale = 3)
    {
        var bitmap = RenderToBitmap(card, scale);
        var dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
        using var stream = System.IO.File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

    private static System.Windows.Shapes.Path DonutSegment(
        double size, double thickness, double from, double to, Color color)
    {
        var center = size / 2;
        var radius = (size - thickness) / 2;
        // 0..1 → degrees from 12 o'clock, clockwise.
        var startAngle = from * 360 - 90;
        var endAngle = to * 360 - 90;
        Point PointAt(double deg)
        {
            var rad = deg * Math.PI / 180;
            return new Point(center + radius * Math.Cos(rad), center + radius * Math.Sin(rad));
        }
        var figure = new PathFigure { StartPoint = PointAt(startAngle), IsClosed = false };
        figure.Segments.Add(new ArcSegment(
            PointAt(endAngle),
            new Size(radius, radius),
            0,
            isLargeArc: endAngle - startAngle > 180,
            SweepDirection.Clockwise,
            isStroked: true));
        return new System.Windows.Shapes.Path
        {
            Data = new PathGeometry(new[] { figure }),
            Stroke = IslandColors.Brush(color),
            StrokeThickness = thickness,
        };
    }

}

using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using AgentIsland.UI.Charts;
using AgentIsland.UI.Theme;

namespace AgentIsland.UI.Interaction;

/// Deliberately tiny Markdown renderer for plan review (A3).
///
/// WPF ships no Markdown, and a full CommonMark engine is far more surface than
/// an approval card needs. This covers the subset agents actually emit in a
/// plan — headings, bullet/ordered lists, fenced code, bold and inline code —
/// and renders anything else as plain text. It never throws on malformed
/// input; an unknown construct simply keeps its literal characters.
internal static class MarkdownLite
{
    public static FrameworkElement Render(string markdown, double maxHeight)
    {
        var panel = new StackPanel { Margin = new Thickness(0) };
        var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var inCode = false;
        var code = new StringBuilder();

        void FlushCode()
        {
            if (code.Length == 0) return;
            var block = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 4, 0, 6),
                Child = new TextBlock
                {
                    Text = code.ToString().TrimEnd('\n'),
                    FontFamily = IslandFonts.Mono,
                    FontSize = 11,
                    Foreground = IslandColors.Brush(IslandColors.White(0.82)),
                    TextWrapping = TextWrapping.NoWrap,
                },
            };
            panel.Children.Add(block);
            code.Clear();
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (inCode) FlushCode();
                inCode = !inCode;
                continue;
            }
            if (inCode)
            {
                code.AppendLine(raw);
                continue;
            }
            if (string.IsNullOrWhiteSpace(line)) continue;

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                var level = trimmed.TakeWhile(c => c == '#').Count();
                var text = trimmed[level..].Trim();
                panel.Children.Add(Block(text, level switch
                {
                    1 => 14,
                    2 => 13,
                    _ => 12,
                }, FontWeights.SemiBold, 0.95, new Thickness(0, level == 1 ? 2 : 8, 0, 3)));
                continue;
            }
            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                panel.Children.Add(Bullet(trimmed[2..], "•"));
                continue;
            }
            var ordered = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(\d+)[.)]\s+(.*)$");
            if (ordered.Success)
            {
                panel.Children.Add(Bullet(ordered.Groups[2].Value, ordered.Groups[1].Value + "."));
                continue;
            }
            panel.Children.Add(Block(line, 12, FontWeights.Normal, 0.78, new Thickness(0, 1, 0, 1)));
        }
        FlushCode();

        return new ScrollViewer
        {
            Content = panel,
            MaxHeight = maxHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 6, 0),
        };
    }

    private static TextBlock Block(string text, double size, FontWeight weight, double opacity, Thickness margin)
    {
        var block = new TextBlock
        {
            FontFamily = IslandFonts.Ui,
            FontSize = size,
            FontWeight = weight,
            Foreground = IslandColors.Brush(IslandColors.White(opacity)),
            TextWrapping = TextWrapping.Wrap,
            Margin = margin,
        };
        AppendInline(block, text);
        return block;
    }

    private static FrameworkElement Bullet(string text, string marker)
    {
        var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var bullet = new TextBlock
        {
            Text = marker,
            FontFamily = IslandFonts.Ui,
            FontSize = 12,
            Foreground = IslandColors.Brush(IslandColors.White(0.45)),
            Margin = new Thickness(2, 0, 7, 0),
        };
        Grid.SetColumn(bullet, 0);
        row.Children.Add(bullet);
        var body = Block(text, 12, FontWeights.Normal, 0.8, new Thickness(0));
        Grid.SetColumn(body, 1);
        row.Children.Add(body);
        return row;
    }

    /// Inline **bold** and `code`, leaving everything else literal.
    private static void AppendInline(TextBlock target, string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            var bold = text.IndexOf("**", index, StringComparison.Ordinal);
            var code = text.IndexOf('`', index);
            var next = -1;
            var isBold = false;
            if (bold >= 0 && (code < 0 || bold < code))
            {
                next = bold;
                isBold = true;
            }
            else if (code >= 0)
            {
                next = code;
            }

            if (next < 0)
            {
                target.Inlines.Add(new Run(text[index..]));
                return;
            }

            if (next > index) target.Inlines.Add(new Run(text[index..next]));

            if (isBold)
            {
                var close = text.IndexOf("**", next + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    target.Inlines.Add(new Run(text[next..]));
                    return;
                }
                target.Inlines.Add(new Run(text[(next + 2)..close]) { FontWeight = FontWeights.SemiBold });
                index = close + 2;
            }
            else
            {
                var close = text.IndexOf('`', next + 1);
                if (close < 0)
                {
                    target.Inlines.Add(new Run(text[next..]));
                    return;
                }
                target.Inlines.Add(new Run(text[(next + 1)..close])
                {
                    FontFamily = IslandFonts.Mono,
                    Foreground = IslandColors.Brush(IslandColors.White(0.9)),
                });
                index = close + 1;
            }
        }
    }
}

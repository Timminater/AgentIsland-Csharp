using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AgentIsland.Backend.Interaction;
using AgentIsland.Core.Interaction;
using AgentIsland.UI.Charts;
using AgentIsland.UI.Localization;
using AgentIsland.UI.Theme;

namespace AgentIsland.UI;

/// Readable transcript + diff viewer (B4).
///
/// A standalone window rather than another carousel page: transcripts are long
/// and read at leisure, which does not fit the island's glanceable surface.
/// Local-only, read-only, and it re-reads on demand instead of watching files.
public sealed class TranscriptWindow : Window
{
    private static TranscriptWindow? _instance;

    /// Entry point. Named ShowWindow so it cannot collide with Window.Show.
    public static void ShowWindow()
    {
        if (_instance is null)
        {
            _instance = new TranscriptWindow();
            _instance.Closed += (_, _) => _instance = null;
        }
        if (!_instance.IsVisible) _instance.Show();
        if (_instance.WindowState == WindowState.Minimized) _instance.WindowState = WindowState.Normal;
        _instance.Activate();
        _instance.Reload();
    }

    private readonly ListBox _list = new();
    private readonly ScrollViewer _scroll = new();
    private readonly StackPanel _content = new();
    private readonly TextBlock _title = new();
    private readonly TextBlock _subtitle = new();
    private IReadOnlyList<TranscriptFile> _files = Array.Empty<TranscriptFile>();

    private TranscriptWindow()
    {
        Title = "AgentIsland — " + L10n.Tr("Transcripts & diffs");
        Width = 1180;
        Height = 760;
        MinWidth = 820;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x0D, 0x11));

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Content = root;

        // Left: session picker.
        var sidebar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x11, 0x12, 0x17)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(10, 12, 10, 12),
        };
        var sideStack = new StackPanel();
        sideStack.Children.Add(new TextBlock
        {
            Text = L10n.Tr("Recent sessions"),
            FontFamily = IslandFonts.Ui,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = IslandColors.Brush(IslandColors.White(0.5)),
            Margin = new Thickness(4, 0, 0, 8),
        });
        _list.Background = Brushes.Transparent;
        _list.BorderThickness = new Thickness(0);
        _list.Foreground = IslandColors.Brush(IslandColors.White(0.85));
        _list.FontFamily = IslandFonts.Ui;
        _list.FontSize = 12;
        _list.SelectionChanged += (_, _) => RenderSelected();
        sideStack.Children.Add(_list);
        sidebar.Child = sideStack;
        Grid.SetColumn(sidebar, 0);
        root.Children.Add(sidebar);

        // Right: transcript body.
        var body = new Grid { Margin = new Thickness(20, 14, 20, 14) };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _title.FontFamily = IslandFonts.Ui;
        _title.FontSize = 15;
        _title.FontWeight = FontWeights.SemiBold;
        _title.Foreground = IslandColors.Brush(IslandColors.White(0.95));
        _subtitle.FontFamily = IslandFonts.Ui;
        _subtitle.FontSize = 11;
        _subtitle.Foreground = IslandColors.Brush(IslandColors.White(0.45));
        _subtitle.Margin = new Thickness(0, 2, 0, 10);
        var header = new StackPanel();
        header.Children.Add(_title);
        header.Children.Add(_subtitle);
        Grid.SetRow(header, 0);
        body.Children.Add(header);

        _scroll.Content = _content;
        _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Grid.SetRow(_scroll, 1);
        body.Children.Add(_scroll);

        Grid.SetColumn(body, 1);
        root.Children.Add(body);
    }

    public void Reload()
    {
        // Snapshot/demo runs must be deterministic and must never capture a
        // developer's real local conversation into CI artifacts.
        _files = Core.AppEnvironment.IsDemo
            ? Array.Empty<TranscriptFile>()
            : TranscriptReader.Recent();
        _list.Items.Clear();
        foreach (var file in _files)
        {
            _list.Items.Add(new ListBoxItem
            {
                Content = $"{file.Provider} · {file.Label}",
                Tag = file,
                Foreground = IslandColors.Brush(IslandColors.White(0.85)),
                Padding = new Thickness(6, 5, 6, 5),
                ToolTip = file.Path,
            });
        }
        if (_list.Items.Count > 0 && _list.SelectedIndex < 0) _list.SelectedIndex = 0;
        else RenderSelected();
    }

    private void RenderSelected()
    {
        _content.Children.Clear();
        if (_list.SelectedItem is not ListBoxItem { Tag: TranscriptFile file })
        {
            _title.Text = L10n.Tr("No transcripts found");
            _subtitle.Text = L10n.Tr("Sessions appear here once an agent has written a transcript on this machine.");
            return;
        }

        var view = TranscriptReader.Read(file.Path);
        _title.Text = file.Label;
        _subtitle.Text = L10n.TrFormat(
            "{0} · {1} turns · {2} tool calls · {3} file edits",
            file.Provider, view.Turns.Count, view.ToolCount, view.EditCount);

        foreach (var turn in view.Turns)
        {
            _content.Children.Add(RenderTurn(turn));
        }
    }

    private static FrameworkElement RenderTurn(TranscriptTurnView turn)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };

        var (accent, label) = turn.Role switch
        {
            TranscriptRole.User => (Color.FromRgb(0x6E, 0xA8, 0xFF), L10n.Tr("You")),
            TranscriptRole.ToolResult => (Color.FromRgb(0x8A, 0x8F, 0x9A), L10n.Tr("Result")),
            _ => (Color.FromRgb(0xC9, 0x92, 0x6A), L10n.Tr("Assistant")),
        };

        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontFamily = IslandFonts.Ui,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = IslandColors.Brush(accent),
            Margin = new Thickness(0, 0, 0, 3),
        });

        if (turn.Text.Length > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = turn.Text,
                FontFamily = IslandFonts.Ui,
                FontSize = 12,
                Foreground = IslandColors.Brush(IslandColors.White(0.85)),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        foreach (var tool in turn.Tools)
        {
            panel.Children.Add(RenderTool(tool));
        }
        return panel;
    }

    private static FrameworkElement RenderTool(ToolCallView tool)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };

        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TextBlock
        {
            Text = tool.Name,
            FontFamily = IslandFonts.Mono,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = IslandColors.Brush(IslandColors.White(0.8)),
        });
        if (tool.Summary is { Length: > 0 } summary)
        {
            header.Children.Add(new TextBlock
            {
                Text = "  " + summary,
                FontFamily = IslandFonts.Ui,
                FontSize = 11,
                Foreground = IslandColors.Brush(IslandColors.White(0.45)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 620,
            });
        }
        panel.Children.Add(header);

        foreach (var edit in tool.Edits)
        {
            panel.Children.Add(RenderEdit(edit));
        }

        if (tool.Edits.Count == 0 && tool.Output is { Length: > 0 } output)
        {
            panel.Children.Add(CodeBlock(output, null));
        }
        return panel;
    }

    private static FrameworkElement RenderEdit(FileEditView edit)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
        panel.Children.Add(new TextBlock
        {
            Text = (edit.IsNewFile ? L10n.Tr("New file") + " · " : "") + edit.Path,
            FontFamily = IslandFonts.Mono,
            FontSize = 11,
            Foreground = IslandColors.Brush(IslandColors.White(0.6)),
            Margin = new Thickness(0, 0, 0, 3),
        });

        var diff = new StackPanel();
        foreach (var line in edit.Lines)
        {
            diff.Children.Add(new Border
            {
                Background = new SolidColorBrush(line.Kind switch
                {
                    DiffLineKind.Added => Color.FromArgb(0x22, 0x3F, 0xB9, 0x50),
                    DiffLineKind.Removed => Color.FromArgb(0x22, 0xE5, 0x4C, 0x4C),
                    _ => Colors.Transparent,
                }),
                Padding = new Thickness(6, 0, 6, 0),
                Child = new TextBlock
                {
                    Text = (line.Kind switch
                    {
                        DiffLineKind.Added => "+ ",
                        DiffLineKind.Removed => "- ",
                        _ => "  ",
                    }) + line.Text,
                    FontFamily = IslandFonts.Mono,
                    FontSize = 11,
                    Foreground = IslandColors.Brush(line.Kind switch
                    {
                        DiffLineKind.Added => Color.FromRgb(0x9C, 0xE0, 0xA6),
                        DiffLineKind.Removed => Color.FromRgb(0xF0, 0x9A, 0x9A),
                        _ => IslandColors.White(0.7),
                    }),
                    TextWrapping = TextWrapping.Wrap,
                },
            });
        }
        panel.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(2),
            Child = diff,
        });
        return panel;
    }

    private static FrameworkElement CodeBlock(string text, Color? _)
    {
        return new ScrollViewer
        {
            MaxHeight = 260,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 4, 0, 4),
                Child = new TextBlock
                {
                    Text = text,
                    FontFamily = IslandFonts.Mono,
                    FontSize = 11,
                    Foreground = IslandColors.Brush(IslandColors.White(0.72)),
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };
    }
}

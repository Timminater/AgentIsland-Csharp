using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AgentIsland.Backend.Interaction;
using AgentIsland.Core;
using AgentIsland.Core.Interaction;
using AgentIsland.UI.Charts;
using AgentIsland.UI.Localization;
using AgentIsland.UI.Providers;
using AgentIsland.UI.Theme;

namespace AgentIsland.UI.Interaction;

/// The prompt card the island shows while an agent is blocked on the user
/// (A1–A5). One card renders every kind — permission, question, plan — because
/// they share a queue and only differ in their body and action row.
public sealed class ApprovalCard : Border
{
    private readonly IApprovalCoordinator _coordinator;
    private readonly StackPanel _body = new();
    private readonly StackPanel _actions = new();
    private readonly TextBlock _title = new();
    private readonly TextBlock _subtitle = new();

    /// Raised after a decision is written, so the host can fold the island.
    public event Action? Resolved;

    public ApprovalCard(IApprovalCoordinator coordinator)
    {
        _coordinator = coordinator;
        CornerRadius = new CornerRadius(14);
        Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x11, 0x12, 0x17));
        BorderBrush = new SolidColorBrush(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF));
        BorderThickness = new Thickness(1);
        Padding = new Thickness(16, 13, 16, 13);
        Margin = new Thickness(12, 8, 12, 8);
        VerticalAlignment = VerticalAlignment.Top;
        Visibility = Visibility.Collapsed;
        IsHitTestVisible = true;

        var stack = new StackPanel();
        Child = stack;

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        stack.Children.Add(header);

        var markHost = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        markHost.Children.Add(new ContentPresenter { Content = null });
        _markHost = markHost;
        Grid.SetColumn(markHost, 0);
        header.Children.Add(markHost);

        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _title.FontFamily = IslandFonts.Ui;
        _title.FontSize = 13;
        _title.FontWeight = FontWeights.SemiBold;
        _title.Foreground = IslandColors.Brush(IslandColors.White(0.95));
        _subtitle.FontFamily = IslandFonts.Ui;
        _title.Margin = new Thickness(0);
        _subtitle.FontSize = 11;
        _subtitle.Foreground = IslandColors.Brush(IslandColors.White(0.45));
        _subtitle.Margin = new Thickness(0, 1, 0, 0);
        titles.Children.Add(_title);
        titles.Children.Add(_subtitle);
        Grid.SetColumn(titles, 1);
        header.Children.Add(titles);

        _body.Margin = new Thickness(0, 10, 0, 0);
        stack.Children.Add(new ScrollViewer
        {
            Content = _body,
            MaxHeight = 330,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        });

        _actions.Orientation = Orientation.Horizontal;
        _actions.HorizontalAlignment = HorizontalAlignment.Right;
        _actions.Margin = new Thickness(0, 12, 0, 0);
        stack.Children.Add(_actions);
    }

    private readonly Grid _markHost;

    /// Rebuild the card for the coordinator's current top request. Hides itself
    /// when there is nothing pending.
    public void Refresh()
    {
        var request = _coordinator.Top;
        if (request is null)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        _markHost.Children.Clear();
        _markHost.Children.Add(ProviderMarks.Mark(request.Provider.ToDisplayProvider(), 20));

        _title.Text = request.Kind switch
        {
            PromptKind.Question => L10n.Tr("The agent is asking you"),
            PromptKind.Plan => L10n.Tr("Review this plan"),
            _ => L10n.TrFormat("{0} wants to run a tool", request.Provider.Display()),
        };

        var parts = new List<string>();
        if (request.SessionLabel is { Length: > 0 } label) parts.Add(label);
        if (request.ToolName is { Length: > 0 } tool) parts.Add(tool);
        _subtitle.Text = string.Join(" · ", parts);

        _body.Children.Clear();
        _actions.Children.Clear();

        switch (request.Kind)
        {
            case PromptKind.Question:
                BuildQuestion(request);
                break;
            case PromptKind.Plan:
                BuildPlan(request);
                break;
            default:
                BuildPermission(request);
                break;
        }

        Visibility = Visibility.Visible;
    }

    private void BuildPermission(ApprovalRequest request)
    {
        if (request.CommandText is { Length: > 0 } command)
        {
            _body.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                Child = new TextBlock
                {
                    Text = command,
                    FontFamily = IslandFonts.Mono,
                    FontSize = 12,
                    Foreground = IslandColors.Brush(IslandColors.White(0.9)),
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 120,
                },
            });
        }

        // A4: flag the dangerous ones before the user presses Allow.
        if (request.Risk != CommandRiskLevel.None && request.RiskReason is { Length: > 0 } reason)
        {
            var critical = request.Risk == CommandRiskLevel.Critical;
            _body.Children.Add(new Border
            {
                Background = new SolidColorBrush(critical
                    ? Color.FromArgb(0x2E, 0xF5, 0x57, 0x4A)
                    : Color.FromArgb(0x26, 0xE8, 0xA3, 0x3D)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 8, 0, 0),
                Child = new TextBlock
                {
                    Text = (critical ? "⚠ " : "! ") + reason,
                    FontFamily = IslandFonts.Ui,
                    FontSize = 11,
                    FontWeight = FontWeights.Medium,
                    Foreground = IslandColors.Brush(critical
                        ? Color.FromRgb(0xFF, 0x8A, 0x80)
                        : Color.FromRgb(0xF0, 0xBE, 0x6E)),
                    TextWrapping = TextWrapping.Wrap,
                },
            });
        }

        // A5: how long the approval lasts.
        var scope = new ComboBox
        {
            Width = 150,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)),
            Foreground = IslandColors.Brush(IslandColors.White(0.85)),
            BorderThickness = new Thickness(0),
            FontFamily = IslandFonts.Ui,
            FontSize = 11,
        };
        scope.Items.Add(L10n.Tr("Allow once"));
        scope.Items.Add(L10n.Tr("Allow for this session"));
        scope.Items.Add(L10n.Tr("Always allow"));
        scope.SelectedIndex = 0;

        var allow = MakeButton(L10n.Tr("Allow"), primary: true);
        allow.Click += (_, _) =>
        {
            var chosen = scope.SelectedIndex switch
            {
                1 => ApprovalScope.Session,
                2 => ApprovalScope.Always,
                _ => ApprovalScope.Once,
            };
            Resolve(request, ApprovalDecisionKind.Allow, chosen);
        };

        var deny = MakeButton(L10n.Tr("Deny"), primary: false);
        deny.Click += (_, _) => Resolve(request, ApprovalDecisionKind.Deny);

        _actions.Children.Add(deny);
        _actions.Children.Add(scope);
        _actions.Children.Add(allow);
    }

    private void BuildQuestion(ApprovalRequest request)
    {
        var questions = request.Questions.Count > 0
            ? request.Questions
            : request.QuestionText is { Length: > 0 } fallback
                ? new[] { new PromptQuestion(fallback, null, false, request.Options) }
                : Array.Empty<PromptQuestion>();
        var editors = new List<(PromptQuestion Question, ListBox Options, TextBox Free)>();

        foreach (var question in questions)
        {
            if (question.Header is { Length: > 0 } header)
            {
                _body.Children.Add(new TextBlock
                {
                    Text = header.ToUpperInvariant(),
                    FontFamily = IslandFonts.Ui,
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = IslandColors.Brush(IslandColors.White(0.45)),
                    Margin = new Thickness(0, editors.Count == 0 ? 0 : 10, 0, 3),
                });
            }
            _body.Children.Add(new TextBlock
            {
                Text = question.Text,
                FontFamily = IslandFonts.Ui,
                FontSize = 12,
                Foreground = IslandColors.Brush(IslandColors.White(0.9)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8),
            });

            var optionList = new ListBox
            {
                SelectionMode = question.MultiSelect ? SelectionMode.Multiple : SelectionMode.Single,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = IslandColors.Brush(IslandColors.White(0.85)),
                FontFamily = IslandFonts.Ui,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4),
            };
            foreach (var option in question.Options)
            {
                optionList.Items.Add(new ListBoxItem { Content = option.Label, ToolTip = option.Value });
            }
            _body.Children.Add(optionList);

            var free = new TextBox
            {
                Background = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)),
                Foreground = IslandColors.Brush(IslandColors.White(0.9)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 6, 8, 6),
                FontFamily = IslandFonts.Ui,
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0),
                MinWidth = 320,
                ToolTip = L10n.Tr("Type another answer"),
            };
            _body.Children.Add(free);
            editors.Add((question, optionList, free));
        }

        var send = MakeButton(L10n.Tr("Send answer"), primary: true);
        send.Click += (_, _) =>
        {
            var answers = new Dictionary<string, string>();
            foreach (var (question, options, free) in editors)
            {
                var answer = free.Text.Trim();
                if (answer.Length == 0)
                {
                    answer = string.Join(", ", options.SelectedItems
                        .OfType<ListBoxItem>()
                        .Select(item => item.Content?.ToString())
                        .Where(value => !string.IsNullOrWhiteSpace(value)));
                }
                if (answer.Length == 0) return;
                answers[question.Text] = answer;
            }
            if (answers.Count > 0)
            {
                Resolve(request, ApprovalDecisionKind.Answer,
                    answer: answers.Values.First(), answers: answers);
            }
        };
        _actions.Children.Add(send);
    }

    private void BuildPlan(ApprovalRequest request)
    {
        _body.Children.Add(MarkdownLite.Render(request.PlanMarkdown ?? string.Empty, maxHeight: 220));

        var feedback = new TextBox
        {
            Background = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)),
            Foreground = IslandColors.Brush(IslandColors.White(0.9)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
            FontFamily = IslandFonts.Ui,
            FontSize = 12,
            Margin = new Thickness(0, 10, 0, 0),
            MinWidth = 320,
        };
        _body.Children.Add(feedback);

        var reject = MakeButton(L10n.Tr("Send feedback"), primary: false);
        reject.Click += (_, _) => Resolve(
            request,
            ApprovalDecisionKind.RejectPlan,
            feedback: feedback.Text.Trim() is { Length: > 0 } text ? text : null);

        var approve = MakeButton(L10n.Tr("Approve plan"), primary: true);
        approve.Click += (_, _) => Resolve(request, ApprovalDecisionKind.ApprovePlan);

        _actions.Children.Add(reject);
        _actions.Children.Add(approve);
    }

    private void Resolve(
        ApprovalRequest request,
        ApprovalDecisionKind kind,
        ApprovalScope scope = ApprovalScope.Once,
        string? answer = null,
        string? feedback = null,
        IReadOnlyDictionary<string, string>? answers = null)
    {
        _coordinator.Respond(request, kind, scope, answer, feedback, answers);
        Refresh();
        Resolved?.Invoke();
    }

    private static Button MakeButton(string label, bool primary)
    {
        var button = new Button
        {
            Content = label,
            FontFamily = IslandFonts.Ui,
            FontSize = 11,
            FontWeight = FontWeights.Medium,
            Foreground = primary
                ? Brushes.White
                : IslandColors.Brush(IslandColors.White(0.78)),
            Background = new SolidColorBrush(primary
                ? Color.FromArgb(0xC8, 0x2E, 0x7D, 0xE0)
                : Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(6, 0, 0, 0),
            Cursor = Cursors.Hand,
            MinWidth = 74,
        };
        return button;
    }
}

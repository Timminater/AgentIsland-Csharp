namespace AgentIsland.Core.Interaction;

/// What kind of decision an agent is waiting on. The island renders a
/// different card for each, but they all travel the same request/response
/// channel.
public enum PromptKind
{
    /// Allow/Deny a tool the agent wants to run (A1).
    Permission,
    /// Pick an option (or type free text) for an AskUserQuestion-style prompt (A2).
    Question,
    /// Approve / reject a plan, optionally with feedback (A3).
    Plan,
}

/// How long an approval lasts. Session grants live in memory only; Always
/// writes a durable rule into the agent's own project settings (A5).
public enum ApprovalScope
{
    Once,
    Session,
    Always,
}

/// The user's answer to a prompt.
public enum ApprovalDecisionKind
{
    Allow,
    Deny,
    /// A question was answered (see ApprovalDecision.Answer).
    Answer,
    ApprovePlan,
    RejectPlan,
    /// The card was dismissed / expired without a decision.
    Dismiss,
}

/// One selectable option of a question prompt.
public sealed record PromptOption(string Label, string Value);

/// One AskUserQuestion entry. Claude can submit one to four questions in a
/// single tool call, so the transport keeps the complete set.
public sealed record PromptQuestion(
    string Text,
    string? Header,
    bool MultiSelect,
    IReadOnlyList<PromptOption> Options);

/// A prompt an agent is blocked on, as seen by the island. Every field is
/// nullable-by-meaning: a Permission prompt fills CommandText, a Question
/// fills QuestionText/Options, a Plan fills PlanMarkdown.
public sealed record ApprovalRequest(
    string Id,
    TriggerTool Provider,
    PromptKind Kind,
    string? ToolName,
    string? CommandText,
    string? QuestionText,
    IReadOnlyList<PromptOption> Options,
    string? PlanMarkdown,
    string? SessionId,
    string? SessionLabel,
    string? Cwd,
    DateTimeOffset CreatedAt,
    CommandRiskLevel Risk,
    string? RiskReason)
{
    public IReadOnlyList<PromptQuestion> Questions { get; init; } =
        QuestionText is { Length: > 0 } text
            ? new[] { new PromptQuestion(text, null, false, Options) }
            : Array.Empty<PromptQuestion>();

    /// Stable key used by session grants: the same provider asking for the
    /// same tool + command in the same session must not re-prompt.
    public string Signature => SignatureOf(Provider, ToolName, CommandText, SessionId, Cwd);

    public static string SignatureOf(
        TriggerTool provider,
        string? toolName,
        string? command,
        string? sessionId = null,
        string? cwd = null) =>
        string.Join('\u001f', provider.ToString(), (toolName ?? "").Trim(),
            (command ?? "").Trim(), (sessionId ?? "").Trim(), (cwd ?? "").Trim());
}

/// The user's decision, written back to the waiting agent.
public sealed record ApprovalDecision(
    string RequestId,
    ApprovalDecisionKind Kind,
    ApprovalScope Scope = ApprovalScope.Once,
    string? Answer = null,
    string? Feedback = null)
{
    public IReadOnlyDictionary<string, string>? Answers { get; init; }

    public static ApprovalDecision Allow(string requestId, ApprovalScope scope = ApprovalScope.Once) =>
        new(requestId, ApprovalDecisionKind.Allow, scope);

    public static ApprovalDecision Deny(string requestId, string? reason = null) =>
        new(requestId, ApprovalDecisionKind.Deny, ApprovalScope.Once, Feedback: reason);

    public static ApprovalDecision Dismiss(string requestId) =>
        new(requestId, ApprovalDecisionKind.Dismiss);
}

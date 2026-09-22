namespace AgentIsland.Core.Interaction;

public enum TranscriptRole
{
    User,
    Assistant,
    ToolResult,
}

public enum DiffLineKind
{
    Context,
    Added,
    Removed,
}

/// One rendered line of a reconstructed file edit.
public sealed record DiffLine(DiffLineKind Kind, string Text);

/// A file the agent changed, reconstructed from the tool call that changed it.
public sealed record FileEditView(string Path, IReadOnlyList<DiffLine> Lines, bool IsNewFile);

/// One tool invocation with whatever we could recover from it.
public sealed record ToolCallView(
    string Name,
    string? Summary,
    IReadOnlyList<FileEditView> Edits,
    string? Output)
{
    public string? Id { get; init; }
}

/// One conversational turn (B4).
public sealed record TranscriptTurnView(
    TranscriptRole Role,
    string Text,
    IReadOnlyList<ToolCallView> Tools);

/// A parsed session transcript ready for the viewer.
public sealed record TranscriptView(
    string Path,
    string Label,
    IReadOnlyList<TranscriptTurnView> Turns)
{
    public int ToolCount => Turns.Sum(t => t.Tools.Count);

    public int EditCount => Turns.Sum(t => t.Tools.Sum(x => x.Edits.Count));
}

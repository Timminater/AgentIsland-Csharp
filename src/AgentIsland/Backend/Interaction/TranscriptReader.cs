using System.IO;
using System.Text.Json;
using AgentIsland.Core.Interaction;
using AgentIsland.Windows;

namespace AgentIsland.Backend.Interaction;

/// A transcript file on disk, as offered in the viewer's picker.
public sealed record TranscriptFile(string Path, string Label, string Provider, DateTimeOffset Modified);

/// Reads local agent transcripts into a readable view (B4).
///
/// Local-first and read-only: it never uploads, never writes, and tolerates a
/// transcript being appended to while it reads (a torn last line is simply
/// skipped). File edits are reconstructed from the tool call that produced
/// them, so what the viewer shows is exactly what the agent asked for.
public static class TranscriptReader
{
    /// Most recent transcripts across the local agent roots, newest first.
    public static IReadOnlyList<TranscriptFile> Recent(int max = 40)
    {
        var files = new List<TranscriptFile>();
        foreach (var root in IslandPaths.ClaudeProjectRoots)
        {
            files.AddRange(Collect(root, "Claude", max));
        }
        files.AddRange(Collect(IslandPaths.CodexSessionsRoot, "Codex", max));

        return files
            .OrderByDescending(f => f.Modified)
            .Take(max)
            .ToList();
    }

    private static IReadOnlyList<TranscriptFile> Collect(string root, string provider, int max)
    {
        var newest = new PriorityQueue<TranscriptFile, long>();
        try
        {
            if (!Directory.Exists(root)) return Array.Empty<TranscriptFile>();
            foreach (var path in Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories))
            {
                FileInfo info;
                try
                {
                    info = new FileInfo(path);
                }
                catch
                {
                    continue;
                }
                if (info.Length == 0) continue;
                var transcript = new TranscriptFile(
                    path,
                    Path.GetFileNameWithoutExtension(path),
                    provider,
                    info.LastWriteTimeUtc);
                newest.Enqueue(transcript, transcript.Modified.UtcTicks);
                if (newest.Count > max) newest.Dequeue();
            }
        }
        catch
        {
        }
        return newest.UnorderedItems.Select(item => item.Element).ToList();
    }

    public static TranscriptView Read(string path, int maxTurns = 400)
    {
        var turns = new List<TranscriptTurnView>();
        // The mutable list objects that the turns also reference, so a later
        // tool_result can be attached to the call that produced it.
        var toolLists = new List<List<ToolCallView>>();
        var toolsById = new Dictionary<string, (List<ToolCallView> List, int Index)>(StringComparer.Ordinal);
        string? label = null;
        var lineCount = 0;

        try
        {
            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (++lineCount > 20000) break;
                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    var type = GetString(root, "type");
                    if (root.TryGetProperty("cwd", out var cwd) && cwd.ValueKind == JsonValueKind.String)
                    {
                        label ??= Path.GetFileName(cwd.GetString() ?? string.Empty);
                    }

                    if (type == "user")
                    {
                        ReadUser(root, turns, toolLists, toolsById);
                    }
                    else if (type == "assistant")
                    {
                        ReadAssistant(root, turns, toolLists, toolsById);
                    }
                }
                catch
                {
                    // Torn or foreign line: keep going.
                }
            }
        }
        catch
        {
        }

        if (turns.Count > maxTurns) turns = turns.TakeLast(maxTurns).ToList();
        return new TranscriptView(
            path,
            string.IsNullOrEmpty(label) ? Path.GetFileNameWithoutExtension(path) : label!,
            turns);
    }

    private static void ReadUser(
        JsonElement root,
        List<TranscriptTurnView> turns,
        List<List<ToolCallView>> toolLists,
        Dictionary<string, (List<ToolCallView> List, int Index)> toolsById)
    {
        if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) return;
        if (!message.TryGetProperty("content", out var content)) return;

        if (content.ValueKind == JsonValueKind.String)
        {
            var text = content.GetString() ?? string.Empty;
            if (text.Trim().Length > 0) turns.Add(new TranscriptTurnView(TranscriptRole.User, text, Array.Empty<ToolCallView>()));
            return;
        }

        if (content.ValueKind != JsonValueKind.Array) return;
        var userText = new List<string>();
        foreach (var item in content.EnumerateArray())
        {
            var itemType = GetString(item, "type");
            if (itemType == "text")
            {
                if (GetString(item, "text") is { Length: > 0 } text) userText.Add(text);
            }
            else if (itemType == "tool_result")
            {
                var output = ReadToolResult(item);
                if (output is null) continue;
                var toolUseId = GetString(item, "tool_use_id");
                if (toolUseId is { Length: > 0 } && toolsById.TryGetValue(toolUseId, out var exact))
                {
                    exact.List[exact.Index] = exact.List[exact.Index] with { Output = output };
                    continue;
                }
                // Attach to the most recent tool call that has no output yet.
                // Records are immutable, so replace the entry inside the very
                // list the turn holds rather than a detached copy.
                for (var li = toolLists.Count - 1; li >= 0; li--)
                {
                    var list = toolLists[li];
                    var attached = false;
                    for (var i = list.Count - 1; i >= 0; i--)
                    {
                        if (list[i].Output is not null) continue;
                        list[i] = list[i] with { Output = output };
                        attached = true;
                        break;
                    }
                    if (attached) break;
                }
            }
        }
        if (userText.Count > 0)
        {
            turns.Add(new TranscriptTurnView(TranscriptRole.User, string.Join("\n", userText), Array.Empty<ToolCallView>()));
        }
    }

    private static void ReadAssistant(
        JsonElement root,
        List<TranscriptTurnView> turns,
        List<List<ToolCallView>> toolLists,
        Dictionary<string, (List<ToolCallView> List, int Index)> toolsById)
    {
        if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) return;
        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) return;

        var text = new List<string>();
        var callViews = new List<ToolCallView>();
        foreach (var item in content.EnumerateArray())
        {
            var itemType = GetString(item, "type");
            if (itemType == "text")
            {
                if (GetString(item, "text") is { Length: > 0 } piece) text.Add(piece);
            }
            else if (itemType == "tool_use")
            {
                var view = ReadToolUse(item);
                callViews.Add(view);
                if (view.Id is { Length: > 0 } id) toolsById[id] = (callViews, callViews.Count - 1);
            }
        }
        if (callViews.Count > 0) toolLists.Add(callViews);
        if (text.Count == 0 && callViews.Count == 0) return;
        turns.Add(new TranscriptTurnView(TranscriptRole.Assistant, string.Join("\n", text), callViews));
    }

    private static string? ReadToolResult(JsonElement item)
    {
        if (!item.TryGetProperty("content", out var content)) return null;
        if (content.ValueKind == JsonValueKind.String) return Truncate(content.GetString());
        if (content.ValueKind != JsonValueKind.Array) return null;
        var parts = new List<string>();
        foreach (var part in content.EnumerateArray())
        {
            if (GetString(part, "text") is { Length: > 0 } text) parts.Add(text);
        }
        return parts.Count == 0 ? null : Truncate(string.Join("\n", parts));
    }

    private static ToolCallView ReadToolUse(JsonElement item)
    {
        var name = GetString(item, "name") ?? "tool";
        var input = item.TryGetProperty("input", out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : default;
        var summary = GetString(input, "description") ?? GetString(input, "command");
        return new ToolCallView(name, Truncate(summary), ReadEdits(name, input), null)
        {
            Id = GetString(item, "id"),
        };
    }

    /// Reconstruct file edits from the edit-shaped tools. Anything else is
    /// shown as a plain tool call.
    private static IReadOnlyList<FileEditView> ReadEdits(string name, JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object) return Array.Empty<FileEditView>();
        var path = GetString(input, "file_path") ?? GetString(input, "path");

        if (name is "Edit" && path is { Length: > 0 })
        {
            var oldText = GetString(input, "old_string") ?? string.Empty;
            var newText = GetString(input, "new_string") ?? string.Empty;
            return new[] { new FileEditView(path, Diff(oldText, newText), false) };
        }
        if (name is "Write" && path is { Length: > 0 })
        {
            var body = GetString(input, "content") ?? string.Empty;
            var lines = SplitLines(body).Select(l => new DiffLine(DiffLineKind.Added, l)).ToList();
            return new[] { new FileEditView(path, lines, true) };
        }
        if (name is "MultiEdit" && path is { Length: > 0 }
            && input.TryGetProperty("edits", out var edits) && edits.ValueKind == JsonValueKind.Array)
        {
            var views = new List<FileEditView>();
            foreach (var edit in edits.EnumerateArray())
            {
                views.Add(new FileEditView(
                    path,
                    Diff(GetString(edit, "old_string") ?? string.Empty, GetString(edit, "new_string") ?? string.Empty),
                    false));
            }
            return views;
        }
        return Array.Empty<FileEditView>();
    }

    private static IReadOnlyList<DiffLine> Diff(string oldText, string newText)
    {
        var oldLines = SplitLines(oldText).ToArray();
        var newLines = SplitLines(newText).ToArray();
        if ((long)oldLines.Length * newLines.Length > 1_000_000)
        {
            return BoundedDiff(oldLines, newLines);
        }

        var lengths = new int[oldLines.Length + 1, newLines.Length + 1];
        for (var i = oldLines.Length - 1; i >= 0; i--)
        for (var j = newLines.Length - 1; j >= 0; j--)
        {
            lengths[i, j] = oldLines[i] == newLines[j]
                ? lengths[i + 1, j + 1] + 1
                : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
        }

        var result = new List<DiffLine>();
        var oi = 0;
        var ni = 0;
        while (oi < oldLines.Length && ni < newLines.Length)
        {
            if (oldLines[oi] == newLines[ni])
            {
                result.Add(new DiffLine(DiffLineKind.Context, oldLines[oi++]));
                ni++;
            }
            else if (lengths[oi + 1, ni] >= lengths[oi, ni + 1])
                result.Add(new DiffLine(DiffLineKind.Removed, oldLines[oi++]));
            else
                result.Add(new DiffLine(DiffLineKind.Added, newLines[ni++]));
        }
        while (oi < oldLines.Length) result.Add(new DiffLine(DiffLineKind.Removed, oldLines[oi++]));
        while (ni < newLines.Length) result.Add(new DiffLine(DiffLineKind.Added, newLines[ni++]));
        return result;
    }

    private static IReadOnlyList<DiffLine> BoundedDiff(string[] oldLines, string[] newLines)
    {
        var prefix = 0;
        while (prefix < oldLines.Length && prefix < newLines.Length
            && oldLines[prefix] == newLines[prefix]) prefix++;
        var suffix = 0;
        while (suffix < oldLines.Length - prefix && suffix < newLines.Length - prefix
            && oldLines[^(suffix + 1)] == newLines[^(suffix + 1)]) suffix++;
        var result = oldLines.Take(prefix).Select(line => new DiffLine(DiffLineKind.Context, line)).ToList();
        result.AddRange(oldLines.Skip(prefix).Take(oldLines.Length - prefix - suffix)
            .Select(line => new DiffLine(DiffLineKind.Removed, line)));
        result.AddRange(newLines.Skip(prefix).Take(newLines.Length - prefix - suffix)
            .Select(line => new DiffLine(DiffLineKind.Added, line)));
        result.AddRange(oldLines.Skip(oldLines.Length - suffix)
            .Select(line => new DiffLine(DiffLineKind.Context, line)));
        return result;
    }

    private static IEnumerable<string> SplitLines(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static string? Truncate(string? text, int max = 4000)
    {
        if (text is null) return null;
        return text.Length <= max ? text : text[..max] + "…";
    }

    private static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
}

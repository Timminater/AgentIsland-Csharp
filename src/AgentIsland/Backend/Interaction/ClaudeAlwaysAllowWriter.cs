using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentIsland.Windows;

namespace AgentIsland.Backend.Interaction;

/// Writes a durable "Always allow" rule into Claude Code's own settings (A5).
///
/// This edits the file the user would otherwise have edited by hand, so it is
/// intentionally conservative: it only ever appends to `permissions.allow`,
/// backs the file up once before the first change, and refuses to create a
/// project `.claude` directory that does not already exist.
public static class ClaudeAlwaysAllowWriter
{
    /// JsonNode.ToJsonString marks its options read-only and then demands a
    /// resolver, so a bare `new JsonSerializerOptions { WriteIndented = true }`
    /// throws at write time.
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
    };

    /// Project-scoped settings when the session has a real Claude project,
    /// otherwise the user-level settings file.
    public static string? SettingsFileFor(string? cwd)
    {
        if (!string.IsNullOrWhiteSpace(cwd))
        {
            var projectDir = Path.Combine(cwd.Trim(), ".claude");
            if (Directory.Exists(projectDir))
            {
                return Path.Combine(projectDir, "settings.local.json");
            }
        }
        var roots = IslandPaths.ClaudeConfigRoots;
        return roots.Count > 0 ? Path.Combine(roots[0], "settings.json") : null;
    }

    /// The permission rule Claude Code understands for this request.
    public static string RuleFor(string? toolName, string? command)
    {
        var tool = string.IsNullOrWhiteSpace(toolName) ? "Bash" : toolName.Trim();
        if (string.Equals(tool, "Bash", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(command))
        {
            return $"Bash({command.Trim()})";
        }
        return tool;
    }

    /// Append the rule. Returns the file written, or null when it could not be
    /// written (missing config root, unreadable JSON, IO error).
    public static string? Add(string? cwd, string? toolName, string? command)
    {
        var path = SettingsFileFor(cwd);
        if (path is null) return null;
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) return null;
            Directory.CreateDirectory(directory);

            JsonObject root;
            if (File.Exists(path))
            {
                Backup(path);
                root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            var permissions = root["permissions"] as JsonObject ?? new JsonObject();
            root["permissions"] = permissions;
            var allow = permissions["allow"] as JsonArray ?? new JsonArray();
            permissions["allow"] = allow;

            var rule = RuleFor(toolName, command);
            foreach (var node in allow)
            {
                if (node is JsonValue value
                    && value.TryGetValue<string>(out var existing)
                    && string.Equals(existing, rule, StringComparison.Ordinal))
                {
                    return path;
                }
            }

            allow.Add(rule);
            var temp = path + ".tmp";
            File.WriteAllText(temp, root.ToJsonString(WriteOptions));
            File.Move(temp, path, overwrite: true);
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static void Backup(string path)
    {
        try
        {
            var backup = path + ".agentisland.bak";
            if (!File.Exists(backup)) File.Copy(path, backup);
        }
        catch
        {
        }
    }
}

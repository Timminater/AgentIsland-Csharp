using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentIsland.Windows;

namespace AgentIsland.Backend.Interaction;

/// Installs/removes the two Claude Code hooks the approval pipeline needs.
///
/// Only the events we actually need are wired, so ordinary tool calls never
/// round-trip through the island:
///   PermissionRequest (all tools)              -> A1 approve/deny + A5 scopes
///   PreToolUse (AskUserQuestion|ExitPlanMode)  -> A2 questions + A3 plan review
///
/// The hook command is this very executable with a mode flag, so there is
/// nothing extra to install or keep in sync. Every write is preceded by a
/// one-time backup and the merge only touches our own entries.
public static class ClaudeHookInstaller
{
    public enum InstallationState { Missing, Partial, Installed, Invalid }

    private static readonly TimeSpan HookTimeout = TimeSpan.FromSeconds(130);

    /// See ClaudeAlwaysAllowWriter: JsonNode.ToJsonString needs a resolver.
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
    };

    /// Identifies our entries inside an otherwise user-owned settings file.
    private static readonly string[] Flags =
    {
        AgentIsland.Interaction.HookCommand.PermissionRequestFlag,
        AgentIsland.Interaction.HookCommand.PreToolUseFlag,
    };

    public static string? SettingsFile
    {
        get
        {
            var roots = IslandPaths.ClaudeConfigRoots;
            return roots.Count > 0 ? Path.Combine(roots[0], "settings.json") : null;
        }
    }

    public static bool IsInstalled(string? settingsPath = null)
        => GetInstallationState(settingsPath) == InstallationState.Installed;

    public static InstallationState GetInstallationState(string? settingsPath = null)
    {
        var path = Resolve(settingsPath);
        if (path is null || !File.Exists(path)) return InstallationState.Missing;
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            var permission = HasHook(root, "PermissionRequest", AgentIsland.Interaction.HookCommand.PermissionRequestFlag);
            var preTool = HasHook(root, "PreToolUse", AgentIsland.Interaction.HookCommand.PreToolUseFlag);
            if (permission && preTool) return InstallationState.Installed;
            return permission || preTool ? InstallationState.Partial : InstallationState.Missing;
        }
        catch
        {
            return InstallationState.Invalid;
        }
    }

    /// Merge our hook entries in. Returns true when the file now contains them.
    public static bool Install(string? settingsPath = null)
    {
        var path = Resolve(settingsPath);
        if (path is null) return false;
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) return false;
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

            var hooks = root["hooks"] as JsonObject ?? new JsonObject();
            root["hooks"] = hooks;

            EnsureHook(hooks, "PermissionRequest", "*", AgentIsland.Interaction.HookCommand.PermissionRequestFlag);
            EnsureHook(hooks, "PreToolUse", "AskUserQuestion|ExitPlanMode", AgentIsland.Interaction.HookCommand.PreToolUseFlag);

            Write(path, root);
            return IsInstalled(path);
        }
        catch
        {
            return false;
        }
    }

    /// Remove only our entries, leaving every other hook untouched.
    public static bool Uninstall(string? settingsPath = null)
    {
        var path = Resolve(settingsPath);
        if (path is null || !File.Exists(path)) return false;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject root) return false;
            if (root["hooks"] is not JsonObject hooks) return true;

            foreach (var eventName in hooks.Select(kv => kv.Key).ToArray())
            {
                if (hooks[eventName] is not JsonArray groups) continue;
                for (var g = groups.Count - 1; g >= 0; g--)
                {
                    if (groups[g] is not JsonObject group) continue;
                    if (group["hooks"] is not JsonArray list) continue;
                    for (var h = list.Count - 1; h >= 0; h--)
                    {
                        if (IsOurs(list[h])) list.RemoveAt(h);
                    }
                    if (list.Count == 0) groups.RemoveAt(g);
                }
                if (groups.Count == 0) hooks.Remove(eventName);
            }

            Write(path, root);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? Resolve(string? overridePath) =>
        string.IsNullOrWhiteSpace(overridePath) ? SettingsFile : overridePath;

    private static void EnsureHook(JsonObject hooks, string eventName, string matcher, string flag)
    {
        var groups = hooks[eventName] as JsonArray ?? new JsonArray();
        hooks[eventName] = groups;

        foreach (var group in groups)
        {
            if (group is not JsonObject entry || entry["hooks"] is not JsonArray list) continue;
            if (list.Any(IsOurs)) return; // already wired
        }

        groups.Add(new JsonObject
        {
            ["matcher"] = matcher,
            ["hooks"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "command",
                    ["command"] = CommandFor(flag),
                    ["timeout"] = (int)HookTimeout.TotalSeconds,
                    ["statusMessage"] = "Waiting for AgentIsland…",
                },
            },
        });
    }

    private static string CommandFor(string flag)
    {
        var exe = Environment.ProcessPath;
        return string.IsNullOrEmpty(exe) ? flag : $"\"{exe}\" {flag}";
    }

    private static bool IsOurs(JsonNode? node)
    {
        if (node is not JsonObject hook) return false;
        if (hook["command"] is not JsonValue value || !value.TryGetValue<string>(out var command)) return false;
        return Flags.Any(flag => command.Contains(flag, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountOurHooks(JsonObject? root)
    {
        if (root?["hooks"] is not JsonObject hooks) return 0;
        var count = 0;
        foreach (var eventEntry in hooks)
        {
            if (eventEntry.Value is not JsonArray groups) continue;
            foreach (var group in groups)
            {
                if (group is not JsonObject entry || entry["hooks"] is not JsonArray list) continue;
                count += list.Count(IsOurs);
            }
        }
        return count;
    }

    private static bool HasHook(JsonObject? root, string eventName, string flag)
    {
        if (root?["hooks"] is not JsonObject hooks
            || hooks[eventName] is not JsonArray groups) return false;
        foreach (var group in groups)
        {
            if (group is not JsonObject entry || entry["hooks"] is not JsonArray list) continue;
            foreach (var node in list)
            {
                if (node is not JsonObject hook
                    || hook["command"] is not JsonValue value
                    || !value.TryGetValue<string>(out var command)) continue;
                if (command.Contains(flag, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }

    private static void Write(string path, JsonObject root)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(WriteOptions));
        File.Move(temp, path, overwrite: true);
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

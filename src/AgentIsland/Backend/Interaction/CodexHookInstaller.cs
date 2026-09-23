using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentIsland.Windows;

namespace AgentIsland.Backend.Interaction;

/// Installs/removes AgentIsland's Codex PermissionRequest hook in Codex's
/// user-level hooks.json, merging with other user hooks in that file.
public static class CodexHookInstaller
{
    public enum InstallationState { Missing, Installed, Invalid }

    private const string Flag = AgentIsland.Interaction.HookCommand.CodexPermissionRequestFlag;
    private static readonly TimeSpan HookTimeout = TimeSpan.FromSeconds(130);
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
    };

    public static string SettingsFile => Path.Combine(IslandPaths.CodexHome, "hooks.json");

    public static bool IsInstalled(string? settingsPath = null) =>
        GetInstallationState(settingsPath) == InstallationState.Installed;

    public static InstallationState GetInstallationState(string? settingsPath = null)
    {
        var path = Resolve(settingsPath);
        if (!File.Exists(path)) return InstallationState.Missing;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject root) return InstallationState.Invalid;
            if (root["hooks"] is not null and not JsonObject) return InstallationState.Invalid;
            if (root["hooks"] is JsonObject hooks
                && hooks["PermissionRequest"] is not null and not JsonArray)
            {
                return InstallationState.Invalid;
            }
            return HasHook(root) ? InstallationState.Installed : InstallationState.Missing;
        }
        catch
        {
            return InstallationState.Invalid;
        }
    }

    public static bool Install(string? settingsPath = null)
    {
        var path = Resolve(settingsPath);
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) return false;
            Directory.CreateDirectory(directory);

            JsonObject root;
            if (File.Exists(path))
            {
                if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject existingRoot) return false;
                root = existingRoot;
                Backup(path);
            }
            else
            {
                root = new JsonObject();
            }

            if (root["hooks"] is not null and not JsonObject) return false;
            var hooks = root["hooks"] as JsonObject ?? new JsonObject();
            root["hooks"] = hooks;
            if (hooks["PermissionRequest"] is not null and not JsonArray) return false;

            var groups = hooks["PermissionRequest"] as JsonArray ?? new JsonArray();
            hooks["PermissionRequest"] = groups;
            if (!HasHookIn(groups))
            {
                var command = CommandFor(Flag);
                groups.Add(new JsonObject
                {
                    ["matcher"] = "*",
                    ["hooks"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "command",
                            ["command"] = command,
                            ["commandWindows"] = command,
                            ["timeout"] = (int)HookTimeout.TotalSeconds,
                            ["statusMessage"] = "Waiting for AgentIsland approval…",
                        },
                    },
                });
            }

            Write(path, root);
            return IsInstalled(path);
        }
        catch
        {
            return false;
        }
    }

    public static bool Uninstall(string? settingsPath = null)
    {
        var path = Resolve(settingsPath);
        if (!File.Exists(path)) return true;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject root) return false;
            if (root["hooks"] is not JsonObject hooks) return true;

            foreach (var eventName in hooks.Select(pair => pair.Key).ToArray())
            {
                if (hooks[eventName] is not JsonArray groups) continue;
                for (var groupIndex = groups.Count - 1; groupIndex >= 0; groupIndex--)
                {
                    if (groups[groupIndex] is not JsonObject group
                        || group["hooks"] is not JsonArray handlers) continue;
                    for (var handlerIndex = handlers.Count - 1; handlerIndex >= 0; handlerIndex--)
                    {
                        if (IsOurs(handlers[handlerIndex])) handlers.RemoveAt(handlerIndex);
                    }
                    if (handlers.Count == 0) groups.RemoveAt(groupIndex);
                }
                if (groups.Count == 0) hooks.Remove(eventName);
            }

            Write(path, root);
            return !HasHook(JsonNode.Parse(File.ReadAllText(path)) as JsonObject);
        }
        catch
        {
            return false;
        }
    }

    private static string Resolve(string? overridePath) =>
        string.IsNullOrWhiteSpace(overridePath) ? SettingsFile : Path.GetFullPath(overridePath);

    private static string CommandFor(string flag)
    {
        var exe = Environment.ProcessPath;
        return string.IsNullOrEmpty(exe) ? flag : $"\"{exe}\" {flag}";
    }

    private static bool HasHook(JsonObject? root) =>
        root?["hooks"] is JsonObject hooks
        && hooks["PermissionRequest"] is JsonArray groups
        && HasHookIn(groups);

    private static bool HasHookIn(JsonArray groups) => groups.Any(group =>
        group is JsonObject entry
        && entry["hooks"] is JsonArray handlers
        && handlers.Any(IsOurs));

    private static bool IsOurs(JsonNode? node)
    {
        if (node is not JsonObject hook) return false;
        return HasFlag(hook["command"]) || HasFlag(hook["commandWindows"]);
    }

    private static bool HasFlag(JsonNode? node) =>
        node is JsonValue value
        && value.TryGetValue<string>(out var command)
        && command.Contains(Flag, StringComparison.OrdinalIgnoreCase);

    private static void Write(string path, JsonObject root)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, root.ToJsonString(WriteOptions));
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch { }
        }
    }

    private static void Backup(string path)
    {
        try
        {
            var backup = path + ".agentisland.bak";
            if (!File.Exists(backup)) File.Copy(path, backup);
        }
        catch { }
    }
}

using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace AgentIsland.Backend.Interaction;

/// Reads Codex's effective hook status through its app-server protocol. The
/// hooks.json file alone cannot tell whether a user trusted a hook.
internal static class CodexHookTrustProbe
{
    internal enum Status { Trusted, Untrusted, Modified, Disabled, Missing, Unknown }

    internal static async Task<Status> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (CodexHookInstaller.GetInstallationState() != CodexHookInstaller.InstallationState.Installed)
            return Status.Missing;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/d /c codex app-server --stdio",
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };

        try
        {
            if (!process.Start()) return Status.Unknown;
            // Drain diagnostics so the child cannot block on a full stderr pipe.
            _ = process.StandardError.ReadToEndAsync(timeout.Token);
            await SendAsync(process, new
            {
                method = "initialize",
                id = 0,
                @params = new
                {
                    clientInfo = new { name = "agentisland", title = "AgentIsland", version = "1" },
                    capabilities = new { experimentalApi = true },
                },
            }, timeout.Token);
            if (await ReadResponseAsync(process, 0, timeout.Token) is not { } initialized
                || initialized.RootElement.TryGetProperty("error", out _)) return Status.Unknown;
            initialized.Dispose();

            await SendAsync(process, new { method = "initialized", @params = new { } }, timeout.Token);
            await SendAsync(process, new
            {
                method = "hooks/list",
                id = 1,
                @params = new { cwds = new[] { Environment.CurrentDirectory } },
            }, timeout.Token);
            using var response = await ReadResponseAsync(process, 1, timeout.Token);
            return response is null ? Status.Unknown
                : ParseResponse(response.RootElement, CodexHookInstaller.SettingsFile);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException
            or System.ComponentModel.Win32Exception or OperationCanceledException or JsonException)
        {
            return Status.Unknown;
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch { }
        }
    }

    internal static Status ParseResponse(string json, string settingsPath)
    {
        using var document = JsonDocument.Parse(json);
        return ParseResponse(document.RootElement, settingsPath);
    }

    private static Status ParseResponse(JsonElement root, string settingsPath)
    {
        if (!root.TryGetProperty("result", out var result)
            || !result.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array) return Status.Unknown;

        var found = false;
        var status = Status.Unknown;
        foreach (var workspace in data.EnumerateArray())
        {
            if (!workspace.TryGetProperty("hooks", out var hooks)
                || hooks.ValueKind != JsonValueKind.Array) continue;
            foreach (var hook in hooks.EnumerateArray())
            {
                if (!EqualsText(hook, "eventName", "permissionRequest")
                    || !EqualsPath(hook, "sourcePath", settingsPath)
                    || !ContainsText(hook, "command", AgentIsland.Interaction.HookCommand.CodexPermissionRequestFlag))
                    continue;

                found = true;
                if (!hook.TryGetProperty("enabled", out var enabled)
                    || enabled.ValueKind != JsonValueKind.True) status = Status.Disabled;
                else if (EqualsText(hook, "trustStatus", "trusted")
                    || EqualsText(hook, "trustStatus", "managed")) return Status.Trusted;
                else if (EqualsText(hook, "trustStatus", "modified")) status = Status.Modified;
                else if (EqualsText(hook, "trustStatus", "untrusted") && status != Status.Modified)
                    status = Status.Untrusted;
            }
        }
        return found ? status : Status.Unknown;
    }

    private static bool EqualsText(JsonElement element, string property, string expected) =>
        element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && string.Equals(value.GetString(), expected, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsText(JsonElement element, string property, string expected) =>
        element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString()!.Contains(expected, StringComparison.OrdinalIgnoreCase);

    private static bool EqualsPath(JsonElement element, string property, string expected)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            return false;
        try { return string.Equals(Path.GetFullPath(value.GetString()!), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    private static async Task SendAsync(Process process, object message, CancellationToken cancellationToken)
    {
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
    }

    private static async Task<JsonDocument?> ReadResponseAsync(Process process, int id, CancellationToken cancellationToken)
    {
        while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
        {
            JsonDocument document;
            try { document = JsonDocument.Parse(line); }
            catch (JsonException) { continue; }
            if (document.RootElement.TryGetProperty("id", out var responseId)
                && responseId.ValueKind == JsonValueKind.Number
                && responseId.GetInt32() == id) return document;
            document.Dispose();
        }
        return null;
    }
}

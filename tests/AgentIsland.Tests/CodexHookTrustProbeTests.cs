using System.Text.Json;
using AgentIsland.Backend.Interaction;

namespace AgentIsland.Tests;

public sealed class CodexHookTrustProbeTests
{
    private const string SettingsPath = @"C:\Users\test\.codex\hooks.json";

    [Theory]
    [InlineData("trusted", true, "Trusted")]
    [InlineData("managed", true, "Trusted")]
    [InlineData("untrusted", true, "Untrusted")]
    [InlineData("modified", true, "Modified")]
    [InlineData("trusted", false, "Disabled")]
    public void ReadsEffectiveAgentIslandHookStatus(
        string trust, bool enabled, string expected)
    {
        Assert.Equal(expected, CodexHookTrustProbe.ParseResponse(
            Response(SettingsPath, "permissionRequest", "agentisland --hook-codex-permission-request", trust, enabled),
            SettingsPath).ToString());
    }

    [Fact]
    public void DoesNotAcceptAnotherHookOrWrongEvent()
    {
        Assert.Equal(CodexHookTrustProbe.Status.Unknown, CodexHookTrustProbe.ParseResponse(
            Response(SettingsPath, "afterToolUse", "agentisland --hook-codex-permission-request", "trusted", true),
            SettingsPath));
        Assert.Equal(CodexHookTrustProbe.Status.Unknown, CodexHookTrustProbe.ParseResponse(
            Response(@"C:\other\hooks.json", "permissionRequest", "agentisland --hook-codex-permission-request", "trusted", true),
            SettingsPath));
        Assert.Equal(CodexHookTrustProbe.Status.Unknown, CodexHookTrustProbe.ParseResponse(
            Response(SettingsPath, "permissionRequest", "unrelated-hook", "trusted", true),
            SettingsPath));
    }

    [Fact]
    public void MissingOrFailedHookListIsUnconfirmed()
    {
        Assert.Equal(CodexHookTrustProbe.Status.Unknown,
            CodexHookTrustProbe.ParseResponse("{\"error\":{\"message\":\"unavailable\"}}", SettingsPath));
        Assert.Equal(CodexHookTrustProbe.Status.Unknown,
            CodexHookTrustProbe.ParseResponse("{\"result\":{\"data\":[{\"hooks\":[]}]}}", SettingsPath));
    }

    private static string Response(string sourcePath, string eventName, string command, string trust, bool enabled) =>
        JsonSerializer.Serialize(new
        {
            result = new
            {
                data = new[]
                {
                    new
                    {
                        cwd = @"C:\Project",
                        hooks = new[] { new { sourcePath, eventName, command, trustStatus = trust, enabled } },
                    },
                },
            },
        });
}

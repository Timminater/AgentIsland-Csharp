using System.IO;
using System.Text.Json;
using AgentIsland.Backend.Interaction;
using AgentIsland.Core;
using AgentIsland.Core.Interaction;
using AgentIsland.UI.Localization;
using Xunit;

namespace AgentIsland.Tests;

/// Covers the approval pipeline (A1–A5), the transcript/diff reader (B4) and
/// the Dutch localization wiring (E7) with real files in a temp directory.
public class InteractionTests
{
    private static string TempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "agentisland-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static ApprovalRequest Permission(string command = "npm run test") =>
        new(
            Guid.NewGuid().ToString("N"),
            TriggerTool.Claude,
            PromptKind.Permission,
            "Bash",
            command,
            null,
            Array.Empty<PromptOption>(),
            null,
            "session-1",
            "checkout-api",
            null,
            DateTimeOffset.UtcNow,
            CommandRisk.Classify(command).Level,
            CommandRisk.Classify(command).Reason);

    // MARK: A4 — destructive command classifier

    [Fact]
    public void CommandRiskFlagsDestructiveCommands()
    {
        Assert.Equal(CommandRiskLevel.Critical, CommandRisk.Classify("rm -rf /").Level);
        Assert.Equal(CommandRiskLevel.Critical, CommandRisk.Classify("git reset --hard HEAD~3").Level);
        Assert.Equal(CommandRiskLevel.Critical, CommandRisk.Classify("DROP TABLE users").Level);
        Assert.Equal(CommandRiskLevel.Critical, CommandRisk.Classify("dd if=/dev/zero of=/dev/sda").Level);
        Assert.NotNull(CommandRisk.Classify("rm -rf /").Reason);

        Assert.Equal(CommandRiskLevel.Warn, CommandRisk.Classify("rm -rf ./build").Level);
        Assert.Equal(CommandRiskLevel.Warn, CommandRisk.Classify("git push --force origin main").Level);
        Assert.Equal(CommandRiskLevel.Warn, CommandRisk.Classify("Remove-Item -Recurse -Force ./dist").Level);

        Assert.Equal(CommandRiskLevel.None, CommandRisk.Classify("npm run test").Level);
        Assert.Equal(CommandRiskLevel.None, CommandRisk.Classify("git status").Level);
        Assert.Equal(CommandRiskLevel.None, CommandRisk.Classify(null).Level);
        Assert.Equal(CommandRiskLevel.None, CommandRisk.Classify("   ").Level);
    }

    [Fact]
    public void SignatureIsStablePerProviderToolAndCommand()
    {
        var first = Permission("npm run test");
        var second = Permission("npm run test");
        Assert.Equal(first.Signature, second.Signature);

        var other = Permission("npm run build");
        Assert.NotEqual(first.Signature, other.Signature);

        Assert.NotEqual(first.Signature, (first with { SessionId = "session-2" }).Signature);
        Assert.NotEqual(first.Signature, (first with { Cwd = @"C:\other-project" }).Signature);
    }

    [Fact]
    public async Task HeartbeatWritesAreSafeWhenRefreshesOverlap()
    {
        var channel = new FileInteractionChannel(TempRoot());
        var writers = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 50; i++) channel.TouchHeartbeat();
        }));
        await Task.WhenAll(writers);
        Assert.True(channel.ConsumerAlive(TimeSpan.FromSeconds(2)));
    }

    // MARK: A1 — spool round-trip

    [Fact]
    public void ChannelRoundTripsRequestAndDecision()
    {
        var channel = new FileInteractionChannel(TempRoot());
        var request = Permission();

        channel.Publish(request);
        var pending = channel.Pending();
        Assert.Single(pending);
        Assert.Equal(request.Id, pending[0].Id);
        Assert.Equal(PromptKind.Permission, pending[0].Kind);
        Assert.Equal("npm run test", pending[0].CommandText);

        channel.Respond(ApprovalDecision.Allow(request.Id));
        var decision = channel.Await(request.Id, TimeSpan.FromSeconds(2));
        Assert.NotNull(decision);
        Assert.Equal(ApprovalDecisionKind.Allow, decision!.Kind);

        // An answered request is no longer pending, and Cleanup removes both.
        channel.Clear(request.Id);
        Assert.Empty(channel.Pending());
        channel.Cleanup(request.Id);
        Assert.Null(channel.Await(request.Id, TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public void AwaitReturnsNullOnTimeout()
    {
        var channel = new FileInteractionChannel(TempRoot());
        Assert.Null(channel.Await("nope", TimeSpan.FromMilliseconds(80)));
    }

    // MARK: A5 — session grants

    [Fact]
    public void SessionGrantsPersistAcrossChannelInstances()
    {
        var root = TempRoot();
        var request = Permission();

        var island = new FileInteractionChannel(root);
        Assert.False(island.IsGranted(request.Signature));
        island.Grant(request.Signature);

        // The hook runs in a different process: a fresh channel must see it.
        var hook = new FileInteractionChannel(root);
        Assert.True(hook.IsGranted(request.Signature));

        hook.ClearGrants();
        Assert.False(island.IsGranted(request.Signature));
    }

    [Fact]
    public void CoordinatorAnswersAndGrantsForSession()
    {
        var root = TempRoot();
        var channel = new FileInteractionChannel(root);
        using var coordinator = new ApprovalCoordinator(channel);
        var request = Permission();

        channel.Publish(request);
        coordinator.Refresh();
        Assert.True(coordinator.HasPending);
        Assert.Equal(request.Id, coordinator.Top!.Id);

        coordinator.Respond(request, ApprovalDecisionKind.Allow, ApprovalScope.Session);

        Assert.False(coordinator.HasPending);
        Assert.True(channel.IsGranted(request.Signature));
        var decision = channel.Await(request.Id, TimeSpan.FromSeconds(2));
        Assert.Equal(ApprovalScope.Session, decision!.Scope);
    }

    [Fact]
    public void CoordinatorDropsExpiredRequests()
    {
        var channel = new FileInteractionChannel(TempRoot());
        using var coordinator = new ApprovalCoordinator(channel);
        var stale = Permission() with { CreatedAt = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(10) };
        channel.Publish(stale);

        coordinator.Refresh();

        Assert.False(coordinator.HasPending);
        Assert.Empty(channel.Pending());
    }

    // MARK: A1 — Claude Code hook installation

    [Fact]
    public void HookInstallerMergesAndRevertsWithoutTouchingUserHooks()
    {
        var path = Path.Combine(TempRoot(), "settings.json");
        File.WriteAllText(path, """
            {
              "permissions": { "allow": ["Bash(npm run test)"] },
              "hooks": {
                "PreToolUse": [
                  { "matcher": "Write", "hooks": [ { "type": "command", "command": "echo hi" } ] }
                ]
              }
            }
            """);

        Assert.False(ClaudeHookInstaller.IsInstalled(path));
        Assert.True(ClaudeHookInstaller.Install(path));
        Assert.True(ClaudeHookInstaller.IsInstalled(path));

        var installed = File.ReadAllText(path);
        Assert.Contains("--hook-claude-permission-request", installed);
        Assert.Contains("--hook-claude-pre-tool-use", installed);
        Assert.Contains("AskUserQuestion|ExitPlanMode", installed);
        Assert.Contains("Bash(npm run test)", installed);
        Assert.Contains("echo hi", installed);

        // Idempotent: a second install must not duplicate our entries.
        Assert.True(ClaudeHookInstaller.Install(path));
        Assert.Equal(1, Count(File.ReadAllText(path), "--hook-claude-permission-request"));
        Assert.Equal(1, Count(File.ReadAllText(path), "--hook-claude-pre-tool-use"));

        Assert.True(ClaudeHookInstaller.Uninstall(path));
        Assert.False(ClaudeHookInstaller.IsInstalled(path));
        var reverted = File.ReadAllText(path);
        Assert.DoesNotContain("--hook-claude", reverted);
        Assert.Contains("Bash(npm run test)", reverted);
        Assert.Contains("echo hi", reverted);
    }

    [Fact]
    public void HookInstallerReportsPartialConfiguration()
    {
        var path = Path.Combine(TempRoot(), "settings.json");
        File.WriteAllText(path, """
            { "hooks": { "PermissionRequest": [ { "matcher": "*", "hooks": [
              { "type": "command", "command": "AgentIsland.exe --hook-claude-permission-request" }
            ] } ] } }
            """);
        Assert.Equal(ClaudeHookInstaller.InstallationState.Partial,
            ClaudeHookInstaller.GetInstallationState(path));
        Assert.False(ClaudeHookInstaller.IsInstalled(path));
        Assert.True(ClaudeHookInstaller.Install(path));
        Assert.Equal(ClaudeHookInstaller.InstallationState.Installed,
            ClaudeHookInstaller.GetInstallationState(path));
    }

    [Fact]
    public void ClaudeQuestionHookKeepsAllQuestionsAndReturnsStructuredAnswers()
    {
        var request = AgentIsland.Interaction.HookCommand.BuildRequest("""
            {
              "tool_name": "AskUserQuestion", "session_id": "session-abc", "cwd": "C:\\repo",
              "tool_input": { "questions": [
                { "question": "Which queue?", "header": "Queue", "multiSelect": false,
                  "options": [ { "label": "Existing", "description": "Reuse it" } ] },
                { "question": "Which checks?", "header": "Checks", "multiSelect": true,
                  "options": [ { "label": "Unit", "description": "Fast" }, { "label": "UI", "description": "Visual" } ] }
              ] }
            }
            """, "PreToolUse");

        Assert.NotNull(request);
        Assert.Equal(2, request!.Questions.Count);
        Assert.True(request.Questions[1].MultiSelect);
        var decision = new ApprovalDecision(request.Id, ApprovalDecisionKind.Answer)
        {
            Answers = new Dictionary<string, string>
            {
                ["Which queue?"] = "Existing",
                ["Which checks?"] = "Unit, UI",
            },
        };
        using var output = JsonDocument.Parse(
            AgentIsland.Interaction.HookCommand.QuestionDecisionJson(request, decision));
        var hook = output.RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("allow", hook.GetProperty("permissionDecision").GetString());
        var updated = hook.GetProperty("updatedInput");
        Assert.Equal(2, updated.GetProperty("questions").GetArrayLength());
        Assert.Equal("Unit, UI", updated.GetProperty("answers").GetProperty("Which checks?").GetString());
    }

    [Fact]
    public void AlwaysAllowWriterAppendsAndDeduplicates()
    {
        var cwd = TempRoot();
        Directory.CreateDirectory(Path.Combine(cwd, ".claude"));

        var written = ClaudeAlwaysAllowWriter.Add(cwd, "Bash", "npm run test");
        Assert.NotNull(written);
        Assert.True(File.Exists(written!));
        Assert.Contains("Bash(npm run test)", File.ReadAllText(written!));

        ClaudeAlwaysAllowWriter.Add(cwd, "Bash", "npm run test");
        Assert.Equal(1, Count(File.ReadAllText(written!), "Bash(npm run test)"));

        Assert.Equal("Bash(rm -rf ./build)", ClaudeAlwaysAllowWriter.RuleFor("Bash", "rm -rf ./build"));
        Assert.Equal("Write", ClaudeAlwaysAllowWriter.RuleFor("Write", null));
    }

    // MARK: B4 — transcript and diff reader

    [Fact]
    public void TranscriptReaderRebuildsTurnsAndFileEdits()
    {
        var path = Path.Combine(TempRoot(), "session.jsonl");
        File.WriteAllLines(path, new[]
        {
            "{\"type\":\"user\",\"cwd\":\"C:\\\\proj\\\\checkout-api\",\"message\":{\"role\":\"user\",\"content\":\"Fix the auth bug\"}}",
            "{\"type\":\"assistant\",\"message\":{\"role\":\"assistant\",\"content\":[{\"type\":\"text\",\"text\":\"Looking into it.\"},{\"type\":\"tool_use\",\"name\":\"Edit\",\"input\":{\"file_path\":\"src/auth.ts\",\"old_string\":\"const x = 1;\",\"new_string\":\"const x = 2;\"}}]}}",
            "{\"type\":\"user\",\"message\":{\"role\":\"user\",\"content\":[{\"type\":\"tool_result\",\"content\":\"ok\"}]}}",
            "not json at all",
        });

        var view = TranscriptReader.Read(path);

        Assert.Equal("checkout-api", view.Label);
        Assert.Equal(2, view.Turns.Count);
        Assert.Equal(TranscriptRole.User, view.Turns[0].Role);
        Assert.Equal("Fix the auth bug", view.Turns[0].Text);

        var assistant = view.Turns[1];
        Assert.Equal(TranscriptRole.Assistant, assistant.Role);
        Assert.Equal("Looking into it.", assistant.Text);
        Assert.Single(assistant.Tools);

        var edit = assistant.Tools[0].Edits[0];
        Assert.Equal("src/auth.ts", edit.Path);
        Assert.False(edit.IsNewFile);
        Assert.Contains(edit.Lines, l => l.Kind == DiffLineKind.Removed && l.Text == "const x = 1;");
        Assert.Contains(edit.Lines, l => l.Kind == DiffLineKind.Added && l.Text == "const x = 2;");
        Assert.Equal("ok", assistant.Tools[0].Output);
        Assert.Equal(1, view.EditCount);
        Assert.Equal(1, view.ToolCount);
    }

    [Fact]
    public void TranscriptReaderMatchesParallelResultsByToolUseIdAndBuildsContextDiff()
    {
        var path = Path.Combine(TempRoot(), "parallel.jsonl");
        File.WriteAllLines(path, new[]
        {
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"id\":\"a\",\"name\":\"Read\",\"input\":{\"path\":\"a.txt\"}},{\"type\":\"tool_use\",\"id\":\"b\",\"name\":\"Edit\",\"input\":{\"file_path\":\"b.txt\",\"old_string\":\"same\\nold\\ntail\",\"new_string\":\"same\\nnew\\ntail\"}}]} }",
            "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"b\",\"content\":\"result-b\"},{\"type\":\"tool_result\",\"tool_use_id\":\"a\",\"content\":\"result-a\"}]}}",
        });

        var tools = TranscriptReader.Read(path).Turns[0].Tools;
        Assert.Equal("result-a", tools[0].Output);
        Assert.Equal("result-b", tools[1].Output);
        var lines = tools[1].Edits[0].Lines;
        Assert.Contains(lines, line => line.Kind == DiffLineKind.Context && line.Text == "same");
        Assert.Contains(lines, line => line.Kind == DiffLineKind.Removed && line.Text == "old");
        Assert.Contains(lines, line => line.Kind == DiffLineKind.Added && line.Text == "new");
    }

    [Fact]
    public void TranscriptReaderTreatsWriteAsNewFile()
    {
        var path = Path.Combine(TempRoot(), "write.jsonl");
        File.WriteAllLines(path, new[]
        {
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"Write\",\"input\":{\"file_path\":\"src/new.ts\",\"content\":\"line one\\nline two\"}}]}}",
        });

        var view = TranscriptReader.Read(path);
        var edit = view.Turns[0].Tools[0].Edits[0];
        Assert.True(edit.IsNewFile);
        Assert.Equal(2, edit.Lines.Count);
        Assert.All(edit.Lines, line => Assert.Equal(DiffLineKind.Added, line.Kind));
    }

    // MARK: E7 — Dutch localization

    [Fact]
    public void DutchLanguageIsWired()
    {
        var previous = L10n.Current;
        try
        {
            L10n.Current = L10n.Language.Dutch;
            Assert.True(L10n.IsDutch);
            Assert.False(L10n.IsChinese);
            Assert.NotEqual("Quit Agent Island", L10n.Tr("Quit Agent Island"));
            // Unknown keys still fall back rather than blanking the UI.
            Assert.Equal("Some unknown key", L10n.Tr("Some unknown key"));
            // Numbers follow the SELECTED language, not the host's culture.
            Assert.Equal("nl-NL", L10n.NumberCulture.Name);
            Assert.Equal("↑ 20,0%", AgentIsland.UI.Report.DailyReportData.FormatDelta(120, 100));

            L10n.Current = L10n.Language.English;
            Assert.Equal("", L10n.NumberCulture.Name);
            Assert.Equal("↑ 20.0%", AgentIsland.UI.Report.DailyReportData.FormatDelta(120, 100));
        }
        finally
        {
            L10n.Current = previous;
        }
    }

    private static int Count(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}

using System.Text;
using System.Text.Json;
using AgentIsland.Backend.Interaction;
using AgentIsland.Core;
using AgentIsland.Core.Interaction;

namespace AgentIsland.Interaction;

/// The agent-facing half of the approval pipeline.
///
/// Agents cannot call back into a running WPF app, so `AgentIsland.exe` runs a
/// second life as a short-lived console command: an agent hook spawns us with
/// a JSON payload on stdin, we publish a request on the spool, block until the
/// island answers, and print the agent's decision document on stdout.
///
/// Two hook events are installed:
///   PermissionRequest (matcher `*`)  -> A1 permission approval + A5 scopes
///   PreToolUse (AskUserQuestion|ExitPlanMode) -> A2 questions + A3 plan review
internal static class HookCommand
{
    public const string PermissionRequestFlag = "--hook-claude-permission-request";
    public const string PreToolUseFlag = "--hook-claude-pre-tool-use";
    public const string CodexPermissionRequestFlag = "--hook-codex-permission-request";
    public const string SimulateFlag = "--simulate-prompt";

    /// How long the hook lets the island think before giving up. When it
    /// expires we print nothing and exit 0, so Claude Code shows its normal
    /// prompt — the user is never answered for without their input.
    private static readonly TimeSpan DecisionTimeout = TimeSpan.FromSeconds(120);

    private static readonly TimeSpan HeartbeatStale = TimeSpan.FromSeconds(12);

    /// True when this process was started as a hook rather than as the app.
    public static bool IsHookInvocation(string[] args) =>
        args.Any(a => string.Equals(a, PermissionRequestFlag, StringComparison.OrdinalIgnoreCase)
            || string.Equals(a, PreToolUseFlag, StringComparison.OrdinalIgnoreCase)
            || string.Equals(a, CodexPermissionRequestFlag, StringComparison.OrdinalIgnoreCase)
            || string.Equals(a, SimulateFlag, StringComparison.OrdinalIgnoreCase));

    /// Run the hook and return the process exit code. Never throws: a broken
    /// hook must not break the agent, it must fall through to the normal
    /// prompt.
    public static int Run(string[] args)
    {
        try
        {
            if (args.Any(a => string.Equals(a, SimulateFlag, StringComparison.OrdinalIgnoreCase)))
            {
                return Simulate(args);
            }
            var permissionRequest = args.Any(a =>
                string.Equals(a, PermissionRequestFlag, StringComparison.OrdinalIgnoreCase));
            var codexPermissionRequest = args.Any(a =>
                string.Equals(a, CodexPermissionRequestFlag, StringComparison.OrdinalIgnoreCase));
            return codexPermissionRequest
                ? Hook("PermissionRequest", TriggerTool.Codex)
                : Hook(permissionRequest ? "PermissionRequest" : "PreToolUse", TriggerTool.Claude);
        }
        catch
        {
            return 0;
        }
    }

    private static int Hook(string hookEvent, TriggerTool provider)
    {
        var payload = Console.In.ReadToEnd();
        var channel = new FileInteractionChannel();

        // No island running — do not make the agent wait.
        if (!channel.ConsumerAlive(HeartbeatStale)) return 0;

        var request = BuildRequest(payload, hookEvent, provider);
        if (request is null) return 0;

        // An "allow for this session" grant answers without waking the island.
        if (request.Kind == PromptKind.Permission && channel.IsGranted(request.Signature))
        {
            WritePermissionRequestDecision("allow", null);
            return 0;
        }

        channel.Publish(request);
        var decision = channel.Await(request.Id, DecisionTimeout);
        channel.Cleanup(request.Id);
        if (decision is null) return 0;

        if (hookEvent == "PermissionRequest") WritePermissionRequestDecision(decision);
        else WritePreToolUseDecision(request, decision);
        return 0;
    }

    /// Test/verification helper: publish a synthetic prompt as if an agent had
    /// asked, so the island UI can be exercised without a live agent.
    private static int Simulate(string[] args)
    {
        var kind = PromptKind.Permission;
        var index = Array.FindIndex(args, a => string.Equals(a, SimulateFlag, StringComparison.OrdinalIgnoreCase));
        if (index >= 0 && index + 1 < args.Length
            && Enum.TryParse<PromptKind>(args[index + 1], ignoreCase: true, out var parsed))
        {
            kind = parsed;
        }

        var channel = new FileInteractionChannel();
        var command = "rm -rf ./build";
        var (risk, reason) = CommandRisk.Classify(command);
        var request = kind switch
        {
            PromptKind.Question => new ApprovalRequest(
                Guid.NewGuid().ToString("N"), TriggerTool.Claude, PromptKind.Question,
                "AskUserQuestion", null, "Which queue should the worker use?",
                new[]
                {
                    new PromptOption("Use the existing jobs queue", "existing"),
                    new PromptOption("Create a dedicated checkout queue", "dedicated"),
                },
                null, "simulated", "checkout-api", Environment.CurrentDirectory,
                DateTimeOffset.UtcNow, CommandRiskLevel.None, null),
            PromptKind.Plan => new ApprovalRequest(
                Guid.NewGuid().ToString("N"), TriggerTool.Claude, PromptKind.Plan,
                "ExitPlanMode", null, null, Array.Empty<PromptOption>(),
                "# Rate limiting for /checkout\n\n1. Add a token bucket keyed by IP and session\n2. Read the limits from config, not constants\n3. Return 429 with Retry-After when empty\n4. Cover the refusal path in the route tests\n\n**Files**\n- src/routes/checkout.ts\n- src/lib/limiter.ts",
                "simulated", "checkout-api", Environment.CurrentDirectory,
                DateTimeOffset.UtcNow, CommandRiskLevel.None, null),
            _ => new ApprovalRequest(
                Guid.NewGuid().ToString("N"), TriggerTool.Claude, PromptKind.Permission,
                "Bash", command, null, Array.Empty<PromptOption>(), null,
                "simulated", "checkout-api", Environment.CurrentDirectory,
                DateTimeOffset.UtcNow, risk, reason),
        };

        channel.Publish(request);
        Console.Error.WriteLine($"published {request.Kind} request {request.Id}");
        var decision = channel.Await(request.Id, DecisionTimeout);
        channel.Cleanup(request.Id);
        Console.Error.WriteLine(decision is null
            ? "no decision (timed out)"
            : $"decision: {decision.Kind} scope={decision.Scope} answer={decision.Answer}");
        return 0;
    }

    internal static ApprovalRequest? BuildRequest(
        string payload,
        string hookEvent,
        TriggerTool provider = TriggerTool.Claude)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var toolName = GetString(root, "tool_name");
        var sessionId = GetString(root, "session_id");
        var cwd = GetString(root, "cwd");
        var toolInput = root.TryGetProperty("tool_input", out var input) && input.ValueKind == JsonValueKind.Object
            ? input
            : default;

        var kind = hookEvent == "PermissionRequest"
            ? PromptKind.Permission
            : toolName switch
            {
                "AskUserQuestion" => PromptKind.Question,
                "ExitPlanMode" => PromptKind.Plan,
                _ => PromptKind.Permission,
            };

        string? command = null;
        string? riskCommand = null;
        string? question = null;
        IReadOnlyList<PromptOption> options = Array.Empty<PromptOption>();
        IReadOnlyList<PromptQuestion> parsedQuestions = Array.Empty<PromptQuestion>();
        string? plan = null;

        if (toolInput.ValueKind == JsonValueKind.Object)
        {
            command = GetString(toolInput, "command");
            riskCommand = command;
            command ??= GetString(toolInput, "description");
            plan = GetString(toolInput, "plan");
            if (kind == PromptKind.Question)
            {
                parsedQuestions = ReadQuestions(toolInput);
                if (parsedQuestions.FirstOrDefault() is { } first)
                {
                    question = first.Text;
                    options = first.Options;
                }
            }
        }

        var (level, reason) = kind == PromptKind.Permission
            ? CommandRisk.Classify(riskCommand)
            : (CommandRiskLevel.None, null);

        return new ApprovalRequest(
            Guid.NewGuid().ToString("N"),
            provider,
            kind,
            toolName,
            command,
            question,
            options,
            plan,
            sessionId,
            sessionId is { Length: > 8 } ? sessionId[..8] : sessionId,
            cwd,
            DateTimeOffset.UtcNow,
            level,
            reason)
        {
            Questions = parsedQuestions,
        };
    }

    private static IReadOnlyList<PromptQuestion> ReadQuestions(JsonElement toolInput)
    {
        if (!toolInput.TryGetProperty("questions", out var questions)
            || questions.ValueKind != JsonValueKind.Array
            || questions.GetArrayLength() == 0)
        {
            return Array.Empty<PromptQuestion>();
        }
        var result = new List<PromptQuestion>();
        foreach (var item in questions.EnumerateArray().Take(4))
        {
            var text = GetString(item, "question");
            if (string.IsNullOrWhiteSpace(text)) continue;
            var options = new List<PromptOption>();
            if (item.TryGetProperty("options", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var option in list.EnumerateArray())
                {
                    var label = GetString(option, "label");
                    if (label is null) continue;
                    options.Add(new PromptOption(label, GetString(option, "description") ?? label));
                }
            }
            var multiSelect = item.TryGetProperty("multiSelect", out var multi)
                && multi.ValueKind is JsonValueKind.True;
            result.Add(new PromptQuestion(text, GetString(item, "header"), multiSelect, options));
        }
        return result;
    }

    private static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    // MARK: - Decision documents

    /// PermissionRequest uses a nested `decision.behavior` object.
    private static void WritePermissionRequestDecision(ApprovalDecision decision)
    {
        switch (decision.Kind)
        {
            case ApprovalDecisionKind.Allow:
                WritePermissionRequestDecision("allow", null);
                break;
            case ApprovalDecisionKind.Deny:
                WritePermissionRequestDecision("deny", decision.Feedback ?? "Denied in AgentIsland");
                break;
            default:
                break;
        }
    }

    private static void WritePermissionRequestDecision(string behavior, string? message)
    {
        var json = new StringBuilder();
        json.Append("{\"hookSpecificOutput\":{\"hookEventName\":\"PermissionRequest\",\"decision\":{\"behavior\":\"");
        json.Append(behavior);
        json.Append('"');
        if (message is { Length: > 0 })
        {
            json.Append(",\"message\":");
            json.Append(JsonSerializer.Serialize(message));
        }
        json.Append("}}}");
        Write(json);
    }

    /// PreToolUse uses a flat `permissionDecision` field. It carries questions
    /// (A2) and plan review (A3).
    private static void WritePreToolUseDecision(ApprovalRequest request, ApprovalDecision decision)
    {
        switch (decision.Kind)
        {
            case ApprovalDecisionKind.Answer:
                WriteQuestionDecision(request, decision);
                break;
            case ApprovalDecisionKind.ApprovePlan:
                WritePreToolUseDecision("allow", "Plan approved in AgentIsland");
                break;
            case ApprovalDecisionKind.RejectPlan:
                WritePreToolUseDecision("deny", decision.Feedback is { Length: > 0 } feedback
                    ? "Plan rejected: " + feedback
                    : "Plan rejected in AgentIsland");
                break;
            case ApprovalDecisionKind.Allow:
                WritePreToolUseDecision("allow", "Approved in AgentIsland");
                break;
            case ApprovalDecisionKind.Deny:
                WritePreToolUseDecision("deny", decision.Feedback ?? "Denied in AgentIsland");
                break;
            default:
                break;
        }
    }

    private static void WritePreToolUseDecision(string decision, string reason)
    {
        var json = new StringBuilder();
        json.Append("{\"hookSpecificOutput\":{\"hookEventName\":\"PreToolUse\",\"permissionDecision\":\"");
        json.Append(decision);
        json.Append("\",\"permissionDecisionReason\":");
        json.Append(JsonSerializer.Serialize(reason));
        json.Append("}}");
        Write(json);
    }

    private static void WriteQuestionDecision(ApprovalRequest request, ApprovalDecision decision)
    {
        Console.Out.Write(QuestionDecisionJson(request, decision));
        Console.Out.Flush();
    }

    internal static string QuestionDecisionJson(ApprovalRequest request, ApprovalDecision decision)
    {
        var answers = decision.Answers is { Count: > 0 }
            ? decision.Answers
            : request.Questions.FirstOrDefault() is { } first && decision.Answer is { Length: > 0 } answer
                ? new Dictionary<string, string> { [first.Text] = answer }
                : new Dictionary<string, string>();

        var questions = request.Questions.Select(question => new
        {
            question = question.Text,
            header = question.Header,
            multiSelect = question.MultiSelect,
            options = question.Options.Select(option => new
            {
                label = option.Label,
                description = option.Value,
            }).ToArray(),
        }).ToArray();

        var document = new
        {
            hookSpecificOutput = new
            {
                hookEventName = "PreToolUse",
                permissionDecision = "allow",
                permissionDecisionReason = "Answered in AgentIsland",
                updatedInput = new { questions, answers },
            },
        };
        return JsonSerializer.Serialize(document);
    }

    private static void Write(StringBuilder json)
    {
        Console.Out.Write(json.ToString());
        Console.Out.Flush();
    }
}

using System.Text.RegularExpressions;

namespace AgentIsland.Core.Interaction;

/// How dangerous a command the agent is asking to run looks. Purely advisory:
/// the classifier never blocks or auto-denies, it only tells the approval card
/// what to shout about before the user presses Allow (A4).
public enum CommandRiskLevel
{
    None,
    Warn,
    Critical,
}

/// Heuristic, data-driven classifier for destructive/irreversible commands.
/// Rules are regexes over the raw command text; the first matching rule wins,
/// so order the table most-specific first. It deliberately errs toward
/// flagging: a false "this deletes files" hint is cheap, a missed one is not.
public static class CommandRisk
{
    private sealed record Rule(Regex Pattern, CommandRiskLevel Level, string Reason);

    private static readonly Rule[] Rules = BuildRules();

    /// Classify a command. Returns None with a null reason for anything the
    /// table does not recognise (including null/empty input).
    public static (CommandRiskLevel Level, string? Reason) Classify(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return (CommandRiskLevel.None, null);
        var text = command.Trim();
        foreach (var rule in Rules)
        {
            if (rule.Pattern.IsMatch(text)) return (rule.Level, rule.Reason);
        }
        return (CommandRiskLevel.None, null);
    }

    private static Rule[] BuildRules()
    {
        // RegexOptions.IgnoreCase | Singleline so a multi-line script still
        // matches; \s covers the line breaks inside a chained command.
        const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.Singleline;

        return new[]
        {
            // ---- Critical: unrecoverable / wipes a system or the home tree.
            new Rule(new Regex(@"\brm\s+(-[a-z]*\s+)*(-[a-z]*r[a-z]*f|-[a-z]*f[a-z]*r)[a-z]*\s+(/|~|\$HOME|\*|\.)(\s|$)", Options),
                CommandRiskLevel.Critical, "Deletes the filesystem, home directory or everything recursively"),
            new Rule(new Regex(@"\brm\s+-[a-z]*r[a-z]*f[a-z]*\s+/\*", Options),
                CommandRiskLevel.Critical, "Recursively force-deletes the root filesystem"),
            new Rule(new Regex(@"\bgit\s+reset\s+--hard\b", Options),
                CommandRiskLevel.Critical, "Discards all uncommitted work irreversibly"),
            new Rule(new Regex(@"\bgit\s+clean\s+-[a-z]*[fd][a-z]*[xX]?\b|\bgit\s+clean\s+-[a-z]*x", Options),
                CommandRiskLevel.Critical, "Deletes untracked and ignored files"),
            new Rule(new Regex(@"\b(drop\s+(database|schema|table)|truncate\s+table)\b", Options),
                CommandRiskLevel.Critical, "Drops or empties a database object"),
            new Rule(new Regex(@"\bmkfs(\.\w+)?\b|\bdd\s+[^\n]*\bof=/dev/", Options),
                CommandRiskLevel.Critical, "Writes directly to a block device, destroying its filesystem"),
            new Rule(new Regex(@":\s*\(\s*\)\s*\{.*\}\s*;\s*:", Options),
                CommandRiskLevel.Critical, "Fork bomb"),
            new Rule(new Regex(@"\bchmod\s+(-[a-z]+\s+)*777\s+/(\s|$)", Options),
                CommandRiskLevel.Critical, "Makes the whole filesystem world-writable"),
            new Rule(new Regex(@"\b(Remove-Item|rm|del|erase)\b[^\n]*-Recurse[^\n]*-Force[^\n]*\b([A-Za-z]:\\?\s*$|[A-Za-z]:\\?\*|/|\$HOME)", Options),
                CommandRiskLevel.Critical, "Recursively force-deletes a drive root or home directory"),
            new Rule(new Regex(@"\b(Format-Volume|Clear-Disk|Initialize-Disk|diskpart\s+clean)\b", Options),
                CommandRiskLevel.Critical, "Formats or wipes a disk"),
            new Rule(new Regex(@"\b(Stop-Computer|Restart-Computer|shutdown\s+(-[a-z]+\s+)*(/s|/r|-h|-r))\b", Options),
                CommandRiskLevel.Critical, "Shuts down or restarts the machine"),

            // ---- Warn: destructive but usually scoped / recoverable.
            new Rule(new Regex(@"\brm\s+-[a-z]*r[a-z]*\b", Options),
                CommandRiskLevel.Warn, "Deletes files recursively"),
            new Rule(new Regex(@"\b(Remove-Item|rmdir|rd)\b[^\n]*-Recurse\b|\bdel\b[^\n]*/s\b|\brmdir\b[^\n]*/s\b", Options),
                CommandRiskLevel.Warn, "Deletes files recursively"),
            new Rule(new Regex(@"\bgit\s+push\b[^\n]*--force(-with-lease)?\b|\bgit\s+push\b[^\n]*\s-f(\s|$)", Options),
                CommandRiskLevel.Warn, "Force-pushes, overwriting remote history"),
            new Rule(new Regex(@"\bgit\s+(checkout|restore)\s+--?\s*\.", Options),
                CommandRiskLevel.Warn, "Discards local changes in the working tree"),
            new Rule(new Regex(@"\bgit\s+branch\s+-D\b", Options),
                CommandRiskLevel.Warn, "Force-deletes a branch"),
            new Rule(new Regex(@"\b(npm|yarn|pnpm)\s+publish\b", Options),
                CommandRiskLevel.Warn, "Publishes a package publicly"),
            new Rule(new Regex(@"\b(truncate\s+-s\s*0|>\s*[^\s|&]+)\s", Options),
                CommandRiskLevel.Warn, "Truncates or overwrites a file"),
            new Rule(new Regex(@"(curl|wget)\b[^\n|]*\|\s*(sudo\s+)?(ba)?sh\b", Options),
                CommandRiskLevel.Warn, "Pipes a remote script straight into a shell"),
            new Rule(new Regex(@"\b(drop\s+column|delete\s+from\b(?!\s+[^\n]*\bwhere\b))", Options),
                CommandRiskLevel.Warn, "Deletes database rows or a column without a WHERE clause"),
        };
    }
}

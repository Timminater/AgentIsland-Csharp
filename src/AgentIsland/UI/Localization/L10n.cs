using System.Globalization;

namespace AgentIsland.UI.Localization;

/// String lookup with an English key namespace. Dutch entries fall back to
/// the English table, then to the key itself.
public static partial class L10n
{
    public enum Language
    {
        Auto,
        English,
        Dutch,
    }

    public static Language Current { get; set; } = Language.Auto;

    public static bool IsDutch => Current switch
    {
        Language.Dutch => true,
        Language.English => false,
        _ => CultureInfo.CurrentUICulture.Name.StartsWith("nl", StringComparison.OrdinalIgnoreCase),
    };

    /// The culture numbers and percentages are formatted with. It follows the
    /// SELECTED language rather than the OS culture, so an English UI never
    /// renders a Dutch decimal comma (and vice versa).
    public static CultureInfo NumberCulture => IsDutch
        ? CultureInfo.GetCultureInfo("nl-NL")
        : CultureInfo.InvariantCulture;

    public static string Tr(string key)
    {
        if (IsDutch)
        {
            // A missing Dutch entry falls back to the English copy rather than
            // the raw key, so a partially translated UI still reads naturally.
            if (DutchTable.TryGetValue(key, out var dutch)) return dutch;
            return EnglishTable.TryGetValue(key, out var fallback) ? fallback : key;
        }
        // The English layer mirrors macOS en.lproj: keys stay historical so
        // call sites never churn, while display copy evolves (trailing full
        // stops stripped, Thread→Session, AgentIsland→Agent Island).
        return EnglishTable.TryGetValue(key, out var english) ? english : key;
    }

    /// Composite-format lookup: the key is an English format string with
    /// {0}-style holes; the selected language table carries the translation.
    public static string TrFormat(string key, params object[] args) =>
        string.Format(Tr(key), args);

    private static readonly Dictionary<string, string> EnglishTable = new()
    {
        ["stalled"] = "paused",
        ["rate limited"] = "usage check paused",
        ["auth required"] = "login required",
        ["Triggers"] = "Auto Resume",
        ["The thread finished. Come back and reply."] = "The session finished. Come back and reply",
        ["Open thread"] = "Open session",
        ["I know"] = "Got it",
        ["Alarm provider"] = "Provider",
        ["Alarm thread"] = "Session",
        ["Alarm project"] = "Project",
        ["A background coding session finished a turn: {0}."] = "A background coding session finished a turn: {0}",
        ["A background coding session finished a turn. It is your turn."] = "A background coding session finished a turn. It is your turn",
        ["today"] = "Today",
        ["Add & enable"] = "Add rule",
        ["After reset"] = "after reset",
        ["No rules yet. Add one to auto-resume a session after the quota resets."] = "No rules yet. Add one to auto-resume a session after the quota resets",
        ["Auto-resume kill switch"] = "Auto-resume master switch",
        ["Status"] = "Status guide",
        ["Launch at login"] = "Launch at Login",
        ["Language saved. Restart Agent Island to apply everywhere."] = "Language saved. Restart Agent Island to apply everywhere",
        ["Auto-Trigger"] = "Auto Resume",
        ["No trusted projects yet."] = "No trusted projects yet",
        ["needs attention"] = "Needs attention",
        ["AgentIsland {0} is currently the newest version available."] = "Agent Island {0} is currently the newest version available",
        ["Couldn't reach the release feed. Try again in a bit."] = "Couldn't reach the release feed. Try again in a bit",
        ["A new version is ready on GitHub Releases. The download is a zip — unpack and replace the app."] = "A new version is ready on GitHub Releases. The download is a zip — unpack and replace the app",
        ["The update downloads in the background, then Agent Island relaunches on the new version."] = "The update downloads in the background, then Agent Island relaunches on the new version",
        ["Automatic update failed. You can download the new version manually from GitHub Releases."] = "Automatic update failed. You can download the new version manually from GitHub Releases",
        ["Usage tiles and top-bar percentages follow this."] = "Usage tiles and top-bar percentages follow this",
        ["Starts after two measurements; pauses hide stale estimates"] = "Starts after two measurements; pauses hide stale estimates",
        ["Estimated empty in {0}, based on the last {1} minutes"] = "Estimated empty in {0}, based on the last {1} minutes",
        ["Below your chosen remaining-quota threshold, the top bar can estimate time to empty from recent usage — and automatically go quiet during pauses"] = "Below your chosen remaining-quota threshold, the top bar can estimate time to empty from recent usage — and automatically go quiet during pauses",
        ["Percent readouts count down what's left of each window rather than up what's spent."] = "Percent readouts count down what's left of each window rather than up what's spent",
        ["Copied! Post it and bring a friend to the island 🏝️ Thanks for spreading the word"] = "Copied! Post it — bring a friend to the island 🏝️",
        ["Saved! Post it and bring a friend to the island 🏝️ Thanks for spreading the word"] = "Saved to file — bring a friend to the island 🏝️",
        ["Details unavailable right now."] = "Details unavailable right now",
        ["Earned resets appear here with their expiry."] = "Earned resets appear here with their expiry",
        ["You're out until it resets at {0}."] = "You're out until it resets at {0}",
        ["You're rate-limited for now."] = "You're rate-limited for now",
        ["Alarm sound"] = "Alert sound",
        ["Open AgentIsland when you sign in."] = "Open Agent Island when you sign in",
        ["How often to refresh."] = "How often to refresh",
        ["Follows the system language."] = "Follows the system language",
        ["Tint the island and pulse the peek pill when 5-hour usage nears your limit."] = "Tint the island and pulse the peek pill when 5-hour usage nears your limit",
        ["Check for new versions in the background and notify you when one's available."] = "Check for new versions in the background and notify you when one's available",
        ["Look for a new version immediately."] = "Look for a new version immediately",
        ["Auto picks the primary display."] = "Auto picks the primary display",
        ["All tokens"] = "All Tokens",
        ["Input, output, and cache."] = "Input, output, and cache",
        ["Input and output only."] = "Input and output only",
        ["Not detected — create a DeepSeek Harness session"] = "Not detected — create a DeepSeek Harness session",
        ["DeepSeek Harness token ledger"] = "DeepSeek Harness token ledger",
        ["API balance"] = "API balance",
        ["available"] = "available",
        ["unavailable"] = "unavailable",
        ["No balance details"] = "No balance details",
        ["granted {0} · topped up {1}"] = "granted {0} · topped up {1}",
        ["balance {0}"] = "balance {0}",
        ["no deepseek api key"] = "DeepSeek API key not configured",
        ["deepseek api key rejected"] = "DeepSeek API key rejected",
        ["account balance not fetched"] = "account balance not fetched",
        ["Peak hours"] = "Peak hours",
        ["Off-peak"] = "Off-peak",
        ["peak"] = "peak",
        ["off-peak"] = "off-peak",
        ["50% off"] = "50% off",
        ["{0} → {1}"] = "{0} → {1}",
        ["Beijing time"] = "Beijing time",
        ["weekend · {0}"] = "weekend · {0}",
        ["Shown on your clock: {0}."] = "Shown on your clock: {0}",
        ["DeepSeek bills peak rates Monday–Friday 09:00–12:00 and 14:00–18:00 Beijing time; every other hour is off-peak at half price."] = "DeepSeek bills peak rates Mon–Fri 09:00–12:00 and 14:00–18:00 Beijing time; every other hour is off-peak at half price",
        ["{0} tokens today"] = "{0} tokens today",
        ["When off, no resume command is ever spawned."] = "When off, no resume command is ever spawned",
        ["Every run, executed or blocked, is logged."] = "Every run, executed or blocked, is logged",
        ["Add a trigger"] = "Add rule",
        ["No triggers yet."] = "No rules yet",
        ["AUTO"] = "Auto",
        ["Pinned to a specific display. Falls back to Auto if unplugged."] = "Pinned to a specific display. Falls back to Auto if unplugged",
        ["A bar at the top of the screen, or a floating widget you drag anywhere."] = "A bar at the top of the screen, or a floating widget you drag anywhere",
        ["Slide the island along its edge to clear tabs and title-bar buttons."] = "Slide the island along its edge to clear tabs and title-bar buttons",
        ["Keep the 5-hour and weekly percentages beside the logos without hovering."] = "Keep the 5-hour and weekly percentages beside the logos without hovering",
        ["When your AI limit resets, auto-send a message so a session keeps running."] = "Resume selected sessions automatically after your usage limit resets",
        ["When off, Agent Island will never spawn Claude or Codex resume commands."] = "When off, Agent Island will never spawn Claude or Codex resume commands",
        ["Records"] = "Run logs",
        ["Open the folder with blocked and executed auto-resume records."] = "Open the folder with blocked and executed resume attempts",
        ["No triggers yet — add one below."] = "No rules yet — add one below",
        ["{0} rule(s) — manage them on the island's Triggers page."] = "{0} rule(s) — manage them on the island's Triggers page",
        ["Include local token cost/value as a swipe page in the island."] = "Include local token cost/value as a swipe page in the island",
        ["What the island's two logos are telling you."] = "What the island's two logos are telling you",
        ["The logo rotates while a session is running."] = "The logo rotates while a session is running",
        ["A thread finished — Agent Island opens an alarm window so you can reply."] = "A session finished — Agent Island opens an alarm window so you can reply",
        ["Limits, login, network, or provider errors make the logo pulse red."] = "Limits, login, network, or provider errors make the logo pulse red",
        ["Pop up a foreground alarm and system notification when a background run needs you."] = "Pop up a foreground alarm and system notification when a background run needs you",
        ["Show thread details"] = "Show session details",
        ["Show session and project names in alarms and notifications."] = "Show session and project names in alarms and notifications",
        ["Choose a built-in sound or use your own file."] = "Choose an alarm tone or use your own audio file",
        ["Adjust how loud the alarm sound is."] = "Adjust how loud the alarm sound is",
        ["New trigger"] = "New resume rule",
        ["Tool"] = "Provider",
        ["Thread"] = "Session to resume",
        ["{0} resets {1}."] = "{0} resets {1}",
        ["{0} reset time unknown."] = "{0} reset time unknown",
        ["Rate"] = "Usage check",
        ["network drop"] = "network drop — showing last data",
        ["TOP Model"] = "TOP MODEL",
        // No ["Cost"] override: it is the island's page tab, which macOS
        // en.lproj renders "Cost". SectionLabel uppercases on its own, so a
        // report-card all-caps entry here bought nothing and renamed the tab.
        ["Share"] = "SHARE",
    };
}

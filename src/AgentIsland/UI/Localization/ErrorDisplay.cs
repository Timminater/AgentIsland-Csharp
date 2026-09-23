namespace AgentIsland.UI.Localization;

/// Provider/network error strings are internal contract identifiers — the
/// activity monitor pattern-matches on the English text — so they stay
/// English in the stores and get localized only at display time.
public static class ErrorDisplay
{
    public static string Localize(string error)
    {
        if (!L10n.IsDutch) return error;
        return error switch
        {
            "auth required — run claude" => "aanmelding vereist — voer claude uit",
            "re-login: claude /login" => "meld opnieuw aan: claude /login",
            // The keychain token 401'd AND the refresh grant failed. Without
            // its own sentinel the caption stayed on the cold-start "auth
            // required" text forever, so a revoked refresh token was
            // indistinguishable from never having signed in (macOS #22).
            "token refresh failed — sign in again" => "vernieuwen van aanmelding mislukt — meld opnieuw aan",
            "no codex auth" => "geen Codex-aanmelding gevonden",
            "auth expired — codex login" => "aanmelding verlopen — voer codex login uit",
            "rate limited" => "snelheidslimiet bereikt",
            "parse error" => "verwerken mislukt",
            "bad response" => "ongeldig antwoord",
            "network timeout" => "netwerktime-out",
            "network drop" => "netwerkverbinding verbroken",
            "no deepseek api key" => "geen DeepSeek API-sleutel ingesteld",
            "deepseek api key rejected" => "DeepSeek API-sleutel geweigerd",
            // ClaudeWebLogin failure reasons. They reach the UI verbatim now
            // that a failed browser round surfaces its reason instead of
            // silently spawning a retired `claude auth login`.
            "login timed out" => "aanmelding duurde te lang",
            "could not open the browser" => "browser kon niet worden geopend",
            "token exchange failed" => "tokenuitwisseling mislukt",
            "state mismatch" => "aanmeldcontrole mislukt — probeer opnieuw",
            "login already in progress" => "er loopt al een aanmelding",
            _ when error.StartsWith("could not open local callback server", StringComparison.Ordinal) =>
                "lokale terugmeldservice kon niet worden gestart" + error["could not open local callback server".Length..],
            _ when error.StartsWith("http ", StringComparison.OrdinalIgnoreCase) => "HTTP-fout " + error[5..],
            _ => error,
        };
    }
}

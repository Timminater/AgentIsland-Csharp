namespace AgentIsland.Core.Interaction;

/// The transport between a blocked agent hook and the island.
///
/// Two processes use it at once: the short-lived hook process the agent
/// spawned (publish a request, block for a decision) and the long-lived UI
/// process (watch requests, write decisions). Implementations must therefore
/// be file- or socket-based and tolerate either side starting or stopping at
/// any time — never in-process state.
public interface IInteractionChannel
{
    /// Requests currently waiting for a decision, oldest first. Used by the
    /// island to render cards; a request whose agent has gone away is dropped
    /// by the implementation's own expiry.
    IReadOnlyList<ApprovalRequest> Pending();

    /// Publish a request so the island can see it (hook side).
    void Publish(ApprovalRequest request);

    /// Block until a decision lands or `timeout` elapses. Returns null on
    /// timeout, which callers MUST treat as "no decision" — never as allow.
    ApprovalDecision? Await(string requestId, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// Write the user's decision (island side).
    void Respond(ApprovalDecision decision);

    /// Drop a request once it has been answered or has expired. The response
    /// file (if any) stays for the hook to consume.
    void Clear(string requestId);

    /// Remove both sides of a request — the hook calls this after reading its
    /// decision so the spool does not grow without bound.
    void Cleanup(string requestId);

    /// Whether a "allow for this session" grant covers this signature. The
    /// hook process asks before publishing, so an already-granted command
    /// never even reaches the island.
    bool IsGranted(string signature);

    /// Persist a session grant (island side). Cleared when the island exits,
    /// which is exactly the "this session" lifetime the user asked for.
    void Grant(string signature);

    /// Drop every session grant (island startup and shutdown).
    void ClearGrants();
}

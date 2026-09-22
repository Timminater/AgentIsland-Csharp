using System.ComponentModel;
using AgentIsland.Core.Interaction;

namespace AgentIsland.Backend.Interaction;

/// Aggregates the prompts agents are blocked on and writes the user's answers
/// back (A1–A3, A5). Owns the spool heartbeat, so the hook side can tell
/// whether an island is running at all.
public interface IApprovalCoordinator : INotifyPropertyChanged
{
    /// Requests waiting for a decision, oldest first.
    IReadOnlyList<ApprovalRequest> Pending { get; }

    /// The request the island surfaces first (oldest), or null.
    ApprovalRequest? Top { get; }

    bool HasPending { get; }

    void Start();

    void Stop();

    /// Re-read the spool now (the UI calls this after a manual refresh).
    void Refresh();

    /// Answer one request. `Session` persists a grant; `Always` also writes a
    /// durable rule into the agent's own settings.
    void Respond(
        ApprovalRequest request,
        ApprovalDecisionKind kind,
        ApprovalScope scope = ApprovalScope.Once,
        string? answer = null,
        string? feedback = null,
        IReadOnlyDictionary<string, string>? answers = null);
}

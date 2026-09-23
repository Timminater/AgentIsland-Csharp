using System.ComponentModel;
using AgentIsland.Core.Interaction;
using AgentIsland.Core.Threading;

namespace AgentIsland.Backend.Interaction;

/// Polls the spool, keeps the island's view of pending prompts current, and
/// routes the user's decisions back. The heartbeat it writes is what lets a
/// hook bail out immediately when no island is running instead of blocking the
/// agent for the full timeout.
public sealed class ApprovalCoordinator : IApprovalCoordinator, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan HeartbeatStale = TimeSpan.FromSeconds(12);

    /// A request older than this had its hook die (the hook's own wait is
    /// shorter); drop it so a stale card cannot linger forever.
    private static readonly TimeSpan MaxRequestAge = TimeSpan.FromMinutes(3);

    private readonly FileInteractionChannel _channel;
    private readonly IUiDispatcher? _dispatcher;
    private readonly object _gate = new();
    private Timer? _timer;
    private int _polling;
    private DateTimeOffset _lastHeartbeat;
    private List<ApprovalRequest> _pending = new();

    public ApprovalCoordinator(FileInteractionChannel? channel = null, IUiDispatcher? dispatcher = null)
    {
        _channel = channel ?? new FileInteractionChannel();
        _dispatcher = dispatcher;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<ApprovalRequest> Pending
    {
        get
        {
            lock (_gate) return _pending.ToArray();
        }
    }

    public ApprovalRequest? Top
    {
        get
        {
            lock (_gate) return _pending.Count > 0 ? _pending[0] : null;
        }
    }

    public bool HasPending
    {
        get
        {
            lock (_gate) return _pending.Count > 0;
        }
    }

    /// Exposed for the hook side and tests.
    public FileInteractionChannel Channel => _channel;

    public void Start()
    {
        // A fresh island owns a fresh "this session": stale grants from a
        // previous run must not auto-approve anything.
        _channel.ClearGrants();
        _channel.TouchHeartbeat();
        _lastHeartbeat = DateTimeOffset.UtcNow;
        _timer ??= new Timer(_ => SafePoll(), null, TimeSpan.Zero, PollInterval);
    }

    public void Stop()
    {
        var timer = Interlocked.Exchange(ref _timer, null);
        timer?.Dispose();
        _channel.ClearGrants();
    }

    public void Refresh() => SafePoll();

    public void Respond(
        ApprovalRequest request,
        ApprovalDecisionKind kind,
        ApprovalScope scope = ApprovalScope.Once,
        string? answer = null,
        string? feedback = null,
        IReadOnlyDictionary<string, string>? answers = null)
    {
        // Codex's PermissionRequest hook can decide this request, but it has
        // no persistent permission-rule output. Never imply "Always" there.
        if (request.Provider == AgentIsland.Core.TriggerTool.Codex && scope == ApprovalScope.Always)
        {
            scope = ApprovalScope.Once;
        }

        if (kind == ApprovalDecisionKind.Allow)
        {
            if (scope == ApprovalScope.Session)
            {
                _channel.Grant(request.Signature);
            }
            else if (scope == ApprovalScope.Always)
            {
                _channel.Grant(request.Signature);
                ClaudeAlwaysAllowWriter.Add(request.Cwd, request.ToolName, request.CommandText);
            }
        }

        _channel.Respond(new ApprovalDecision(request.Id, kind, scope, answer, feedback)
        {
            Answers = answers,
        });
        _channel.Clear(request.Id);
        SafePoll();
    }

    private void SafePoll()
    {
        if (Interlocked.Exchange(ref _polling, 1) != 0) return;
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastHeartbeat >= HeartbeatInterval)
            {
                _channel.TouchHeartbeat();
                _lastHeartbeat = now;
            }
            var fresh = new List<ApprovalRequest>();
            foreach (var request in _channel.Pending())
            {
                if (now - request.CreatedAt > MaxRequestAge)
                {
                    _channel.Clear(request.Id);
                    continue;
                }
                fresh.Add(request);
            }
            Publish(fresh);
        }
        catch
        {
            // Timer callbacks must never terminate the process. The next beat
            // retries; channel readers already tolerate locked/foreign files.
        }
        finally
        {
            Volatile.Write(ref _polling, 0);
        }
    }

    private void Publish(List<ApprovalRequest> next)
    {
        bool changed;
        lock (_gate)
        {
            changed = !Same(_pending, next);
            _pending = next;
        }
        if (!changed) return;

        void Raise()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Pending)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Top)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasPending)));
        }

        if (_dispatcher is { } dispatcher && !dispatcher.CheckAccess()) dispatcher.BeginInvoke(Raise);
        else Raise();
    }

    private static bool Same(IReadOnlyList<ApprovalRequest> left, IReadOnlyList<ApprovalRequest> right)
    {
        if (left.Count != right.Count) return false;
        for (var i = 0; i < left.Count; i++)
        {
            if (left[i].Id != right[i].Id) return false;
        }
        return true;
    }

    public void Dispose() => Stop();
}

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;
using AgentIsland.Core.Interaction;
using AgentIsland.Windows;

namespace AgentIsland.Backend.Interaction;

/// File-spool transport shared by the hook process and the island.
///
/// A request is a JSON file in `requests\`; the answer is a JSON file with the
/// same id in `responses\`. Files (not a socket) because the two sides are
/// different processes with independent lifetimes: the hook is spawned and
/// dies with the agent's turn, the island runs for days. Everything is written
/// to a temp file and moved into place so a reader never sees a half-written
/// JSON document.
public sealed class FileInteractionChannel : IInteractionChannel
{
    private static readonly ConcurrentDictionary<string, object> WriteLocks =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true,
    };

    private readonly string _requests;
    private readonly string _responses;
    private readonly string _grants;
    private readonly string _aliveFile;

    public FileInteractionChannel(string? root = null)
    {
        var resolved = root ?? Path.Combine(IslandPaths.AppSupportDir, "interaction");
        _requests = Path.Combine(resolved, "requests");
        _responses = Path.Combine(resolved, "responses");
        _grants = Path.Combine(resolved, "grants");
        _aliveFile = Path.Combine(resolved, "alive");
    }

    /// The island stamps this every few seconds. A hook that finds it stale
    /// knows nobody will answer and can bail out immediately instead of
    /// making the agent wait out the full timeout.
    public void TouchHeartbeat()
    {
        EnsureDirectories();
        WriteAtomic(_aliveFile, DateTimeOffset.UtcNow.ToString("O"));
    }

    public bool ConsumerAlive(TimeSpan staleAfter)
    {
        try
        {
            if (!File.Exists(_aliveFile)) return false;
            if (!DateTimeOffset.TryParse(File.ReadAllText(_aliveFile), out var stamp)) return false;
            return DateTimeOffset.UtcNow - stamp <= staleAfter;
        }
        catch
        {
            return false;
        }
    }

    public void Publish(ApprovalRequest request)
    {
        EnsureDirectories();
        WriteAtomic(RequestPath(request.Id), JsonSerializer.Serialize(request, Json));
    }

    public IReadOnlyList<ApprovalRequest> Pending()
    {
        var list = new List<ApprovalRequest>();
        if (!Directory.Exists(_requests)) return list;
        foreach (var file in Directory.EnumerateFiles(_requests, "*.json"))
        {
            try
            {
                var request = JsonSerializer.Deserialize<ApprovalRequest>(File.ReadAllText(file), Json);
                if (request is null) continue;
                // An answered request is no longer pending even if the hook
                // has not cleaned up yet.
                if (File.Exists(ResponsePath(request.Id))) continue;
                list.Add(request);
            }
            catch
            {
                // A torn/foreign file must never take the island down.
            }
        }
        return list.OrderBy(r => r.CreatedAt).ToList();
    }

    public ApprovalDecision? Await(string requestId, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        var path = ResponsePath(requestId);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                try
                {
                    var decision = JsonSerializer.Deserialize<ApprovalDecision>(File.ReadAllText(path), Json);
                    if (decision is not null) return decision;
                }
                catch
                {
                    // Torn read: retry until the timeout expires.
                }
            }
            Thread.Sleep(60);
        }
        return null;
    }

    public void Respond(ApprovalDecision decision)
    {
        EnsureDirectories();
        WriteAtomic(ResponsePath(decision.RequestId), JsonSerializer.Serialize(decision, Json));
    }

    public void Clear(string requestId)
    {
        // Only the request: the response is the hook's to read and consume.
        TryDelete(RequestPath(requestId));
    }

    public void Cleanup(string requestId)
    {
        TryDelete(RequestPath(requestId));
        TryDelete(ResponsePath(requestId));
    }

    public bool IsGranted(string signature) =>
        !string.IsNullOrEmpty(signature) && File.Exists(GrantPath(signature));

    public void Grant(string signature)
    {
        if (string.IsNullOrEmpty(signature)) return;
        Directory.CreateDirectory(_grants);
        WriteAtomic(GrantPath(signature), DateTimeOffset.UtcNow.ToString("O"));
    }

    public void ClearGrants()
    {
        try
        {
            if (!Directory.Exists(_grants)) return;
            foreach (var file in Directory.EnumerateFiles(_grants, "*.grant"))
            {
                TryDelete(file);
            }
        }
        catch
        {
        }
    }

    /// One short filename per signature; the signature itself can contain
    /// characters (and newlines) that no filesystem wants.
    private string GrantPath(string signature)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(signature));
        return Path.Combine(_grants, Convert.ToHexString(bytes)[..32] + ".grant");
    }

    private string RequestPath(string id) => Path.Combine(_requests, Sanitize(id) + ".json");

    private string ResponsePath(string id) => Path.Combine(_responses, Sanitize(id) + ".json");

    /// Ids come from the hook (a GUID) but never trust them as path segments.
    private static string Sanitize(string id)
    {
        var chars = id.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray();
        return chars.Length == 0 ? Guid.NewGuid().ToString("N") : new string(chars);
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(_requests);
        Directory.CreateDirectory(_responses);
    }

    private static void WriteAtomic(string path, string content)
    {
        // Heartbeat and manual refreshes can write the same destination at
        // the same time. A unique sibling keeps both writes atomic without
        // making the writers contend for one shared .tmp file.
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        lock (WriteLocks.GetOrAdd(Path.GetFullPath(path), static _ => new object()))
        {
            try
            {
                File.WriteAllText(temp, content);
                File.Move(temp, path, overwrite: true);
            }
            finally
            {
                TryDelete(temp);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
        }
    }
}

using System.Collections.Concurrent;

namespace BlazorSemanticCompare.Services;

/// <summary>
/// In-memory map from agent code to the live circuit session. Only valid with a single
/// replica: a POST must reach the instance that holds the circuit.
/// </summary>
public sealed class AgentSessionRegistry
{
    private const int MaxFailuresPerWindow = 10;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _failures = new();

    public void Add(AgentSession session) => _sessions[session.Code] = session;

    public void Remove(AgentSession session) => _sessions.TryRemove(session.Code, out _);

    public AgentSession? Find(string code) =>
        _sessions.TryGetValue(AgentSession.Normalize(code), out var session) ? session : null;

    public bool IsBlocked(string client)
    {
        if (!_failures.TryGetValue(client, out var queue)) return false;
        lock (queue)
        {
            Trim(queue);
            return queue.Count >= MaxFailuresPerWindow;
        }
    }

    public void RecordFailure(string client)
    {
        var queue = _failures.GetOrAdd(client, _ => new Queue<DateTime>());
        lock (queue)
        {
            Trim(queue);
            queue.Enqueue(DateTime.UtcNow);
        }
    }

    private static void Trim(Queue<DateTime> queue)
    {
        var cutoff = DateTime.UtcNow - FailureWindow;
        while (queue.Count > 0 && queue.Peek() < cutoff) queue.Dequeue();
    }
}

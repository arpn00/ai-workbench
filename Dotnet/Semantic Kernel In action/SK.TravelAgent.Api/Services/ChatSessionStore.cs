using Microsoft.SemanticKernel.ChatCompletion;
using System.Collections.Concurrent;

namespace SK.TravelAgent.Api.Services;

/// <summary>
/// In-memory store that maps a session ID to its ChatHistory.
/// Thread-safe for multi-request scenarios. Sessions survive for the
/// lifetime of the process (suitable for demo / dev use).
/// </summary>
public sealed class ChatSessionStore
{
    /// <summary>
    /// Orchestration rules injected as the system message for every new session.
    /// </summary>
    private const string SystemPrompt = """
        You are a travel assistant orchestrator with access to two functions:

        1. process_query  – Always call this FIRST with the user's raw input.
           It validates the query and returns structured JSON.
           If the returned JSON has "isTravelQuery": false, stop here and relay
           the rejectionMessage to the user. Do NOT call plan_travel.

        2. plan_travel – Call this ONLY when process_query confirms isTravelQuery=true.
           Pass the full JSON string returned by process_query as the input argument.

        After both functions complete, present the final travel plan to the user
        in a clear, readable format.

        Use the conversation history to understand follow-up questions and references
        to previous trips discussed in the same session.
        """;

    private readonly ConcurrentDictionary<string, ChatHistory> _sessions = new();

    /// <summary>
    /// Returns (or creates) the ChatHistory for the given session.
    /// <paramref name="resolvedId"/> is set to the canonical session ID (auto-generated when null).
    /// </summary>
    public ChatHistory GetOrCreate(string? sessionId, out string resolvedId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            sessionId = Guid.NewGuid().ToString("N");

        resolvedId = sessionId;

        return _sessions.GetOrAdd(sessionId, _ => new ChatHistory(SystemPrompt));
    }

    /// <summary>Explicitly removes a session (e.g. for a "clear history" endpoint).</summary>
    public bool Remove(string sessionId) => _sessions.TryRemove(sessionId, out _);

    /// <summary>Current number of active sessions.</summary>
    public int ActiveSessionCount => _sessions.Count;
}

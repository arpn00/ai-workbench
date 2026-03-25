using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using SK.TravelAgent.Api.Models;

namespace SK.TravelAgent.Api.Services;

/// <summary>
/// Stores detailed trace events for the active HTTP request and session.
/// Uses HttpContext.Items so traces stay isolated per request.
/// </summary>
public sealed class AgentTraceContext
{
    private const string EventsKey = "Trace.Events";
    private const string SessionIdKey = "Trace.SessionId";
    private const string TurnIdKey = "Trace.TurnId";

    private static readonly string[] SensitiveTokens =
    [
        "api_key",
        "apikey",
        "authorization",
        "bearer",
        "token",
        "secret",
        "password"
    ];

    private readonly IHttpContextAccessor _httpContextAccessor;

    public AgentTraceContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void BeginTurn(string sessionId)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return;

        httpContext.Items[SessionIdKey] = sessionId;
        httpContext.Items[TurnIdKey] = Guid.NewGuid().ToString("N");
        httpContext.Items[EventsKey] = new ConcurrentQueue<TraceEvent>();
    }

    public IReadOnlyList<TraceEvent> GetSnapshot()
    {
        var queue = GetQueue();
        return queue is null ? [] : queue.ToArray();
    }

    public void AddUserInput(string payload) =>
        Add("user.input", payload: payload);

    public void AddAssistantOutput(string payload) =>
        Add("assistant.output", payload: payload);

    public void AddAgentInput(string agentName, string payload) =>
        Add("agent.input", agentName: agentName, payload: payload);

    public void AddAgentOutput(string agentName, string payload, double? durationMs = null) =>
        Add("agent.output", agentName: agentName, payload: payload, durationMs: durationMs);

    public void AddToolStart(string pluginName, string functionName, string payload) =>
        Add("tool.start", pluginName: pluginName, functionName: functionName, payload: payload);

    public void AddToolEnd(string pluginName, string functionName, string payload, double? durationMs = null) =>
        Add("tool.end", pluginName: pluginName, functionName: functionName, payload: payload, durationMs: durationMs);

    private void Add(
        string type,
        string? agentName = null,
        string? pluginName = null,
        string? functionName = null,
        string? payload = null,
        double? durationMs = null)
    {
        ConcurrentQueue<TraceEvent>? queue = GetQueue();
        if (queue is null)
            return;

        string sessionId = GetSessionId();
        string turnId = GetTurnId();

        queue.Enqueue(new TraceEvent
        {
            Type = type,
            SessionId = sessionId,
            TurnId = turnId,
            AgentName = agentName,
            PluginName = pluginName,
            FunctionName = functionName,
            Payload = Sanitize(payload),
            DurationMs = durationMs
        });
    }

    private ConcurrentQueue<TraceEvent>? GetQueue()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items[EventsKey] is ConcurrentQueue<TraceEvent> queue)
            return queue;

        return null;
    }

    private string GetSessionId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items[SessionIdKey] is string sessionId && !string.IsNullOrWhiteSpace(sessionId))
            return sessionId;

        return "n/a";
    }

    private string GetTurnId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items[TurnIdKey] is string turnId && !string.IsNullOrWhiteSpace(turnId))
            return turnId;

        return "n/a";
    }

    private static string? Sanitize(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return payload;

        string trimmed = payload.Trim();
        if (SensitiveTokens.Any(token => trimmed.Contains(token, StringComparison.OrdinalIgnoreCase)))
            return "[REDACTED]";

        if (trimmed.Length > 600)
            return string.Concat(trimmed.AsSpan(0, 600), "...");

        return trimmed;
    }
}

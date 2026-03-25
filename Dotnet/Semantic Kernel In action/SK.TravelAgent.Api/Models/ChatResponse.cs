namespace SK.TravelAgent.Api.Models;

/// <summary>
/// Response from POST /api/chat.
/// </summary>
public sealed class ChatResponse
{
    /// <summary>
    /// Session ID — echo this back in subsequent requests to continue the same conversation.
    /// </summary>
    public string SessionId { get; init; } = default!;

    /// <summary>
    /// The travel assistant's reply to the user's message.
    /// </summary>
    public string Reply { get; init; } = default!;

    /// <summary>
    /// Indicates whether detailed tracing was captured for this response.
    /// </summary>
    public bool TraceEnabled { get; init; }

    /// <summary>
    /// Ordered trace events showing agent and tool activity for demo/observability.
    /// </summary>
    public IReadOnlyList<TraceEvent> Trace { get; init; } = [];
}

/// <summary>
/// A single trace event emitted during a chat turn.
/// </summary>
public sealed class TraceEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public string Type { get; init; } = default!;
    public string SessionId { get; init; } = default!;
    public string TurnId { get; init; } = default!;
    public string? AgentName { get; init; }
    public string? PluginName { get; init; }
    public string? FunctionName { get; init; }
    public string? Payload { get; init; }
    public double? DurationMs { get; init; }
}

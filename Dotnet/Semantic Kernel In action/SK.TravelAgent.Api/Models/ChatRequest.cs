namespace SK.TravelAgent.Api.Models;

/// <summary>
/// Payload sent to POST /api/chat.
/// </summary>
public sealed class ChatRequest
{
    /// <summary>
    /// Identifies the ongoing conversation. Omit (or leave empty) on the first turn —
    /// the server will create a new session and echo the generated ID in the response.
    /// Include the returned ID on every subsequent turn to preserve chat history.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// The user's natural-language travel query or follow-up message.
    /// </summary>
    public required string Message { get; set; }
}

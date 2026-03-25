using Microsoft.AspNetCore.Mvc;
using SK.TravelAgent.Api.Models;
using SK.TravelAgent.Api.Services;

namespace SK.TravelAgent.Api.Controllers;

/// <summary>
/// Single chat endpoint for the LLM-orchestrated travel agent.
///
/// POST /api/chat
///   Body  : { "sessionId": "...", "message": "Plan a 5-day trip to Paris" }
///   Reply : { "sessionId": "...", "reply": "Here is your itinerary ..." }
///
/// Session flow
/// ─────────────
/// First turn  : omit sessionId (or send null/empty). The server creates a new
///               session and returns the generated ID in the response.
/// Follow-up   : include the sessionId returned on the previous turn. The full
///               ChatHistory (system prompt + every prior exchange + tool traces)
///               is replayed to the LLM so it has complete conversational context.
/// Clear chat  : DELETE /api/chat/{sessionId}
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class ChatController : ControllerBase
{
    private readonly TravelChatService _chatService;
    private readonly ChatSessionStore _sessionStore;

    public ChatController(TravelChatService chatService, ChatSessionStore sessionStore)
    {
        _chatService = chatService;
        _sessionStore = sessionStore;
    }

    /// <summary>
    /// Send a travel query or follow-up message to the agent.
    /// Returns the assistant reply and a session ID for continuing the conversation.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message cannot be empty." });

        ChatResponse response = await _chatService.ChatAsync(request.SessionId, request.Message);
        return Ok(response);
    }

    /// <summary>
    /// Clear the conversation history for the given session.
    /// </summary>
    [HttpDelete("{sessionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(string sessionId)
    {
        bool removed = _sessionStore.Remove(sessionId);
        return removed ? NoContent() : NotFound(new { error = $"Session '{sessionId}' not found." });
    }
}

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SK.TravelAgent.Api.Models;

namespace SK.TravelAgent.Api.Services;

/// <summary>
/// Handles a single chat turn:
///   1. Resolves (or creates) the session's ChatHistory.
///   2. Appends the new user message.
///   3. Calls the LLM with FunctionChoiceBehavior.Auto so the model can invoke
///      process_query and plan_travel automatically.
///   4. SK updates the ChatHistory in-place with tool-call messages and the
///      assistant reply, preserving the full multi-turn context for future calls.
///   5. Returns the assistant's final text along with the session ID.
/// </summary>
public sealed class TravelChatService
{
    private readonly Kernel _kernel;
    private readonly ChatSessionStore _sessionStore;
    private readonly ILogger<TravelChatService> _logger;
    private readonly AgentTraceContext _traceContext;

    public TravelChatService(
        Kernel kernel,
        ChatSessionStore sessionStore,
        ILogger<TravelChatService> logger,
        AgentTraceContext traceContext)
    {
        _kernel = kernel;
        _sessionStore = sessionStore;
        _logger = logger;
        _traceContext = traceContext;
    }

    public async Task<ChatResponse> ChatAsync(string? sessionId, string userMessage)
    {
        ChatHistory history = _sessionStore.GetOrCreate(sessionId, out string resolvedId);
        _traceContext.BeginTurn(resolvedId);
        _traceContext.AddUserInput(userMessage);

        _logger.LogInformation(
            "Session {SessionId} — received message ({Length} chars)",
            resolvedId, userMessage.Length);

        // Append user turn to persistent history
        history.AddUserMessage(userMessage);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0,
            // Let the LLM decide which kernel functions to call and in what order
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var chatService = _kernel.GetRequiredService<IChatCompletionService>();

        // SK processes the whole ChatHistory (system + all previous turns + new user message).
        // During auto function-calling, SK internally appends tool-call requests, tool results,
        // and the final assistant reply directly into `history`, so every subsequent call
        // carries the complete conversation context automatically.
        IReadOnlyList<ChatMessageContent> newMessages =
            await chatService.GetChatMessageContentsAsync(history, executionSettings, _kernel);

        // Pick the last assistant text as the visible reply
        string reply = newMessages
            .LastOrDefault(m => m.Role == AuthorRole.Assistant && !string.IsNullOrWhiteSpace(m.Content))
            ?.Content
            ?? "I could not process your request. Please try again.";

        _traceContext.AddAssistantOutput(reply);

        IReadOnlyList<TraceEvent> traceEvents = _traceContext.GetSnapshot();

        _logger.LogInformation(
            "Session {SessionId} — reply ({Length} chars), history depth: {Depth}",
            resolvedId, reply.Length, history.Count);

        return new ChatResponse
        {
            SessionId = resolvedId,
            Reply = reply,
            TraceEnabled = true,
            Trace = traceEvents
        };
    }
}

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Demo.Common;
using Travel_Agent.Agents;
using Travel_Agent.Services;

namespace Travel_Agent;

/// <summary>
/// Automatic Function Calling demo using Semantic Kernel.
///
/// BEFORE (manual orchestration):
///   Program.cs called Agent 1 → ran OrchestrationPlanner.Decide → called Agent 2 in a retry loop.
///
/// AFTER (LLM-driven orchestration):
///   Both agents are registered as kernel functions. A single chat completion call with
///   FunctionChoiceBehavior.Auto() lets the LLM decide to call process_query first,
///   inspect the result, and invoke plan_travel only for valid travel queries.
/// </summary>
internal class Program
{
    static async Task Main(string[] args)
    {
        PrintBanner();

        // Load Azure OpenAI configuration
        AzureOpenAISettings settings = DemoConfiguration.LoadAzureOpenAISettings();
        if (!settings.IsConfigured)
        {
            WriteError("Azure OpenAI settings are missing.");
            Console.WriteLine("Set AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_API_KEY before running.");
            return;
        }

        // Single shared Kernel instance for all agents + plugins
        Kernel sharedKernel = KernelFactory.CreateKernel(settings);

        // ── Register both agents as callable Kernel functions ────────────────
        // Wrapping each agent method as a KernelFunction lets the LLM invoke
        // them automatically via tool-calling without any manual orchestration code.
        var agent1 = new QueryProcessorAgent(sharedKernel);
        var agent2 = new TravelPlannerAgent(sharedKernel);

        KernelFunction processQueryFn = KernelFunctionFactory.CreateFromMethod(
            method: (string input) => InvokeWithDemoLoggingAsync(
                functionName: "process_query",
                input: input,
                executeAsync: () => agent1.ProcessQueryAsync(input)),
            functionName: "process_query",
            description: "Validates the user query as travel-related and extracts structured JSON " +
                         "(destination, days, budgetInr). Returns isTravelQuery=false for non-travel queries.");

        KernelFunction planTravelFn = KernelFunctionFactory.CreateFromMethod(
            method: (string input) => InvokeWithDemoLoggingAsync(
                functionName: "plan_travel",
                input: input,
                executeAsync: () => agent2.PlanTravelAsync(input)),
            functionName: "plan_travel",
            description: "Evaluates trip feasibility and generates a day-wise itinerary. " +
                         "Input must be the structured JSON produced by process_query.");

        sharedKernel.Plugins.Add(
            KernelPluginFactory.CreateFromFunctions("TravelAgents", [processQueryFn, planTravelFn]));

        Console.WriteLine("🤖 LLM-Orchestrated Agent System Ready");
        Console.WriteLine("   TravelAgents.process_query  → QueryProcessorAgent");
        Console.WriteLine("   TravelAgents.plan_travel     → TravelPlannerAgent");
        Console.WriteLine("   The LLM decides which functions to call and in what order.");
        Console.WriteLine();

        // ── Get user input ───────────────────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Enter your travel request in natural language:");
        Console.WriteLine("Examples:");
        Console.WriteLine("  - I want to visit Paris for 7 days with a budget of $1000");
        Console.WriteLine("  - Plan a 5-day trip to Tokyo with 80000 INR");
        Console.WriteLine("  - What's the weather like today? (non-travel query for testing guardrails)");
        Console.ResetColor();
        Console.Write("\n> ");

        string userInput = Console.ReadLine() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userInput))
        {
            WriteError("No input provided.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine(new string('=', 80));
        Console.WriteLine();

        // ── Single invocation with automatic function calling ────────────────
        // The system prompt instructs the LLM on the orchestration rules.
        // FunctionChoiceBehavior.Auto() enables the LLM to invoke registered
        // kernel functions as tool calls — replacing the manual planner loop.
        const string systemPrompt = """
            You are a travel assistant orchestrator with access to two functions:

            1. process_query  – Always call this FIRST with the user's raw input.
               It validates the query and returns structured JSON.
               If the returned JSON has "isTravelQuery": false, stop here and relay
               the rejectionMessage to the user. Do NOT call plan_travel.

            2. plan_travel – Call this ONLY when process_query confirms isTravelQuery=true.
               Pass the full JSON string returned by process_query as the input argument.

            After both functions complete, present the final travel plan to the user
            in a clear, readable format.
            """;

        var chatHistory = new ChatHistory(systemPrompt);
        chatHistory.AddUserMessage(userInput);

        // FunctionChoiceBehavior.Auto() — the LLM drives the orchestration
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("🔄 Invoking LLM with automatic function calling...");
        Console.WriteLine("   (FunctionCallTraceFilter will print each tool call as it happens)");
        Console.ResetColor();
        Console.WriteLine();

        var chatService = sharedKernel.GetRequiredService<IChatCompletionService>();
        IReadOnlyList<ChatMessageContent> responses = await chatService.GetChatMessageContentsAsync(
            chatHistory,
            executionSettings,
            sharedKernel);

        // The final assistant message contains the formatted travel plan
        string finalResponse = string.Join("\n", responses
            .Where(r => r.Role == AuthorRole.Assistant && !string.IsNullOrWhiteSpace(r.Content))
            .Select(r => r.Content));

        Console.WriteLine(new string('=', 80));
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✅ Final Response:");
        Console.ResetColor();
        Console.WriteLine(finalResponse);
        Console.WriteLine();

        Console.WriteLine(new string('=', 80));
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("✨ Orchestration Complete! (LLM-driven via Automatic Function Calling)");
        Console.ResetColor();
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    static void PrintBanner()
    {
        Console.Title = "Semantic Kernel - Automatic Function Calling Demo";
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(new string('=', 80));
        Console.WriteLine("  Semantic Kernel - Automatic Function Calling Demo");
        Console.WriteLine("  Manual Orchestration → LLM-Driven Orchestration");
        Console.WriteLine(new string('=', 80));
        Console.ResetColor();
        Console.WriteLine();
    }

    static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static async Task<string> InvokeWithDemoLoggingAsync(
        string functionName,
        string input,
        Func<Task<string>> executeAsync)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"🔧 Function Call: {functionName}");
        Console.ResetColor();

        Console.WriteLine("Input:");
        Console.WriteLine(string.IsNullOrWhiteSpace(input) ? "<empty>" : input);
        Console.WriteLine();

        string output = await executeAsync();

        Console.WriteLine("Output:");
        Console.WriteLine(string.IsNullOrWhiteSpace(output) ? "<empty>" : output);
        Console.WriteLine(new string('-', 33));
        Console.WriteLine();

        return output;
    }
}

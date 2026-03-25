using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SK.TravelAgent.Api.Agents;

/// <summary>
/// Agent 1 — validates the raw user query and normalises the travel parameters
/// into a structured JSON payload for Agent 2.
/// </summary>
public sealed class QueryProcessorAgent
{
    private readonly Kernel _kernel;

    private const string AgentPrompt = """
        Persona:
        You are QueryProcessorAgent.

        Mission:
        Convert raw user text into a strict JSON payload for trip planning.

        Tools:
        - CurrencyConverter.ConvertToDestinationCurrencyAsync(destination, amountInr)

        Rules:
        1) Guardrail first:
           - If the query is not about travel planning, return:
             {
               "isTravelQuery": false,
               "rejectionMessage": "This request is not related to travel planning.",
               "destination": "",
               "days": 0,
               "budgetInr": 0,
               "originalBudget": "",
               "normalizationNote": ""
             }

        2) If travel-related, extract:
           - destination
           - days (default 5)
           - originalBudget text

        3) Normalize budget to INR:
           - If input is already INR, keep as is.
           - If currency is USD/EUR/GBP, convert to INR using:
             USD->83, EUR->90, GBP->104.

        4) Plugin usage requirement:
           - For travel queries, always call CurrencyConverter.ConvertToDestinationCurrencyAsync
             with the final destination and budgetInr.
           - Put plugin output into normalizationNote.

        Output:
        - Return ONLY valid JSON.
        - No markdown. No extra text.

        User Query: {{$input}}
        """;

    public QueryProcessorAgent(Kernel kernel) => _kernel = kernel;

    public async Task<string> ProcessQueryAsync(string userQuery)
    {
        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0,
            FunctionChoiceBehavior = FunctionChoiceBehavior.None()
        };

        KernelFunction fn = _kernel.CreateFunctionFromPrompt(
            AgentPrompt,
            executionSettings: settings,
            functionName: "ProcessQuery",
            description: "Processes query, applies guardrails, and normalizes travel input");

        FunctionResult result = await _kernel.InvokeAsync(fn,
            new KernelArguments { ["input"] = userQuery });

        return result.GetValue<string>() ?? "{}";
    }
}

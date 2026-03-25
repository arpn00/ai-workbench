using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Travel_Agent.Agents;

/// <summary>
/// Agent 2: Travel Planner
/// Responsibilities:
/// - Uses structured JSON from Agent 1
/// - Calls TravelPlannerPlugin.CheckFeasibilityAsync
/// - Produces alternatives when infeasible or itinerary when feasible
/// </summary>
public sealed class TravelPlannerAgent
{
    private readonly Kernel _kernel;

    private const string AgentPrompt = """
        Persona:
        You are TravelPlannerAgent.

        Input:
        You receive JSON from QueryProcessorAgent.

        Tools:
        - TravelPlanner.CheckFeasibilityAsync(destination, days, budgetInr, convertedBudgetContext)

        Rules:
        1) Parse input JSON and read destination, days, budgetInr, normalizationNote.
        2) Call TravelPlanner.CheckFeasibilityAsync exactly once.
        3) If verdict starts with NOT_FEASIBLE:
           - Return feasible=false
           - Provide two alternatives:
             - reducedDaysSuggestion
             - increasedBudgetSuggestionInr
           - Keep itinerary empty.
        4) If verdict starts with FEASIBLE:
           - Return feasible=true
           - Provide a simple day-wise itinerary list.

        Output:
        - Return ONLY valid JSON with this shape:
          {
            "feasible": true,
            "analysis": "...",
            "estimatedTotalCostInr": 0,
            "alternative": {
              "reducedDaysSuggestion": 0,
              "increasedBudgetSuggestionInr": 0
            },
            "itinerary": ["Day 1: ..."],
            "finalRecommendation": "..."
          }

        Structured Travel Data: {{$input}}
        """;

    public TravelPlannerAgent(Kernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<string> PlanTravelAsync(string structuredData)
    {
        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.2,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        KernelFunction plannerFunction = _kernel.CreateFunctionFromPrompt(
            AgentPrompt,
            executionSettings: settings,
            functionName: "PlanTravel",
            description: "Evaluates feasibility and generates response");

        FunctionResult result = await _kernel.InvokeAsync(
            plannerFunction,
            new KernelArguments { ["input"] = structuredData });

        return result.GetValue<string>() ?? "{}";
    }
}

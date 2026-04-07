using Demo.Common;
using FunctionCalling.Plugins;
using Microsoft.SemanticKernel;
using NativePlugins.Plugins;
using SK.TravelAgent.Api.Agents;

namespace SK.TravelAgent.Api.Services;

public static class KernelFactory
{
    /// <summary>
    /// Builds and fully configures a shared Kernel instance:
    ///   - Azure OpenAI chat completion backend
    ///   - FunctionCallTraceFilter for structured logging
    ///   - CurrencyConverter + TravelPlanner native plugins
    ///   - QueryProcessorAgent + TravelPlannerAgent wrapped as kernel functions
    ///     so the LLM can orchestrate them via automatic function calling
    /// </summary>
    public static Kernel Create(
        AzureOpenAISettings settings,
        IServiceProvider serviceProvider)
    {
        Kernel kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(
                deploymentName: settings.DeploymentName,
                endpoint: settings.Endpoint!,
                apiKey: settings.ApiKey!)
            .Build();

        kernel.FunctionInvocationFilters.Add(
            serviceProvider.GetRequiredService<FunctionCallTraceFilter>());

        // Native plugins — deterministic helpers + LLM-powered feasibility check
        kernel.ImportPluginFromObject(new CurrencyConverterPlugin(), "CurrencyConverter");
        kernel.ImportPluginFromObject(new TravelPlannerPlugin(), "TravelPlanner");

        var agent1 = new QueryProcessorAgent(kernel);
        var agent2 = new TravelPlannerAgent(kernel);

        KernelFunction processQueryFn = KernelFunctionFactory.CreateFromMethod(
            method: (string input) => agent1.ProcessQueryAsync(input),
            functionName: "process_query",
            description: "Validates the user query as travel-related and extracts structured JSON " +
                         "(destination, days, budgetInr). Returns isTravelQuery=false for non-travel queries.");

        KernelFunction planTravelFn = KernelFunctionFactory.CreateFromMethod(
            method: (string input) => agent2.PlanTravelAsync(input),
            functionName: "plan_travel",
            description: "Evaluates trip feasibility and generates a day-wise itinerary. " +
                         "Input must be the structured JSON produced by process_query.");

        kernel.Plugins.Add(
            KernelPluginFactory.CreateFromFunctions("TravelAgents", [processQueryFn, planTravelFn]));

        return kernel;
    }
}

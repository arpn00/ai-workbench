using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace NativePlugins.Plugins;

/// <summary>
/// Native plugin — teaches two things in one function:
///
///   Part 1 (C#)  : calculates daily budget from the total INR amount.
///                  Deterministic math — no LLM needed.
///
///   Part 2 (LLM) : asks the LLM whether that daily amount is realistic
///                  for the destination. The LLM has world knowledge; C# does not.
///                  The Kernel is injected automatically — no manual wiring.
///
/// Returns a single verdict string: "FEASIBLE" or "NOT_FEASIBLE".
/// The caller decides what to do next based solely on that verdict.
/// </summary>
public sealed class TravelPlannerPlugin
{
    [KernelFunction]
    [Description(
        "Checks whether an INR budget is realistically feasible for a trip. " +
        "Calculates the daily amount in C#, then asks the LLM to judge feasibility " +
        "based on the destination's cost of living. " +
        "Returns 'FEASIBLE: <reason>' or 'NOT_FEASIBLE: <reason>'.")]
    public async Task<string> CheckFeasibilityAsync(
        [Description("Destination country or city, e.g. 'Paris' or 'Goa'")]
        string destination,

        [Description("Number of trip days (positive integer)")]
        int days,

        [Description("Total trip budget in INR, e.g. 50000")]
        int budgetInr,

        // SK injects the Kernel automatically — this is what lets us call the LLM from inside the plugin.
        Kernel kernel,

        [Description("Converted budget summary from CurrencyConverter, e.g. 'INR 150,000 ≈ EUR 1,650'")]
        string? convertedBudgetContext = null)
    {
        // ── Part 1: C# — arithmetic only, instant, zero LLM tokens ──────────
        double dailyInr = Math.Round((double)budgetInr / days, 0);

        // ── Part 2: LLM — feasibility judgement using world knowledge ────────
        const string feasibilityPrompt = """
            A traveller has a daily budget of INR {{$daily}} for a trip to {{$destination}}.
            Converted budget context: {{$convertedBudgetContext}}
            Is this budget realistically feasible for a basic but comfortable trip?

            Reply in EXACTLY this format (one line only):
            FEASIBLE: with valid reasons
            or
            NOT_FEASIBLE: with valid reasons
            """;

        var result = await kernel.InvokeAsync(
            kernel.CreateFunctionFromPrompt(feasibilityPrompt),
            new KernelArguments
            {
                ["daily"]                  = dailyInr,
                ["destination"]            = destination,
                ["convertedBudgetContext"] = convertedBudgetContext ?? "Not provided",
            });

        return result.GetValue<string>()?.Trim() ?? "NOT_FEASIBLE: Could not determine feasibility.";
    }
}

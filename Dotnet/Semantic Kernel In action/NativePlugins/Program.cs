using Demo.Common;
using Microsoft.SemanticKernel;
using NativePlugins.Plugins;
using System.Diagnostics;

namespace NativePlugins;

internal class Program
{
    private static readonly string[] AllowedStyles = ["relaxed", "adventure", "culture", "family", "mixed"];
    private const int MaxTripDays = 14;

    static async Task Main(string[] args)
    {
        Console.Title = "Semantic Kernel - Native Plugins Demo";
        PrintBanner();

        AzureOpenAISettings settings = DemoConfiguration.LoadAzureOpenAISettings();
        if (!settings.IsConfigured)
        {
            WriteError("Azure OpenAI settings are missing.");
            Console.WriteLine("Set AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_API_KEY before running.");
            return;
        }

        // ── Build the kernel ─────────────────────────────────────────────────────
        var kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatClient(
                deploymentName: settings.DeploymentName,
                endpoint: settings.Endpoint!,
                apiKey: settings.ApiKey!)
            .Build();

        // ── Register the native plugin ───────────────────────────────────────────
        kernel.ImportPluginFromObject(new TravelPlannerPlugin(), "TravelPlanner");

        // ── Collect user input ───────────────────────────────────────────────────
        string destination = ConsolePrompt.Required("Destination (country or city) : ");
        int    days        = ReadTripDays();
        int    budgetInr   = ConsolePrompt.PositiveInt("Total budget in INR          : ");
        string style       = ReadTravelStyle();
        Console.WriteLine();

        // ── Step 1: invoke the native plugin ────────────────────────────────────
        // Part 1 inside the plugin  -> C# calculates the daily INR amount (math, no LLM)
        // Part 2 inside the plugin  -> LLM judges whether that amount is realistic
        // The Kernel is injected automatically; this calling code never changes.
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[Step 1] Calling native plugin  ->  TravelPlanner.CheckFeasibility");
        Console.WriteLine("         C# calculates daily budget. LLM judges feasibility.");
        Console.ResetColor();
        Console.WriteLine();

        var pluginResult = await kernel.InvokeAsync(
            "TravelPlanner",
            "CheckFeasibility",
            new KernelArguments
            {
                ["destination"] = destination,
                ["days"]        = days,
                ["budgetInr"]   = budgetInr,
            });

        string verdict = pluginResult.GetValue<string>()?.Trim() ?? "NOT_FEASIBLE: Unknown error.";

        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine($"Plugin verdict: {verdict}");
        Console.ResetColor();
        Console.WriteLine();

        // ── Step 2: always call the main LLM prompt ─────────────────────────────
        // FEASIBLE     -> generate itinerary
        // NOT_FEASIBLE -> return a clear "not feasible" response with brief guidance
        bool isFeasible = verdict.StartsWith("FEASIBLE", StringComparison.OrdinalIgnoreCase);

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(isFeasible
            ? "[Step 2] Budget is feasible - generating itinerary..."
            : "[Step 2] Budget not feasible - asking LLM for budget viability response...");
        Console.ResetColor();
        Console.WriteLine();

        const string itineraryPrompt = """
            You are an expert travel planner.

            Trip details:
            - Destination : {{$destination}}
            - Duration    : {{$days}} days
            - Total budget: INR {{$budgetInr}}
            - Travel style: {{$style}}

            Plugin feasibility verdict:
            {{$verdict}}

            If verdict starts with FEASIBLE:
            - Write a concise day-by-day itinerary that fits within this budget.

            If verdict starts with NOT_FEASIBLE:
            - Do NOT generate an itinerary.
            - Start with: "Not feasible in this budget."
            - Then provide exactly 3 short bullet points explaining why and how much to increase budget.
            """;

        var travelFunction = kernel.CreateFunctionFromPrompt(itineraryPrompt);

        var timer = Stopwatch.StartNew();
        FunctionResult itineraryResult;
        try
        {
            itineraryResult = await kernel.InvokeAsync(travelFunction, new KernelArguments
            {
                ["destination"] = destination,
                ["days"]        = days,
                ["budgetInr"]   = budgetInr,
                ["style"]       = style,
                ["verdict"]     = verdict,
            });
        }
        catch (Exception ex)
        {
            WriteError("Failed to generate itinerary.");
            Console.WriteLine($"  Reason : {ex.Message}");
            return;
        }
        finally
        {
            timer.Stop();
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(isFeasible ? "=== Travel Itinerary ===" : "=== Budget Feasibility Response ===");
        Console.ResetColor();
        Console.WriteLine(new string('-', 70));
        Console.WriteLine(itineraryResult.GetValue<string>());
        Console.WriteLine(new string('-', 70));
        Console.WriteLine($"Completed in {timer.ElapsedMilliseconds} ms.");
        Console.WriteLine();
        Console.Write("Press any key to exit...");
        Console.ReadKey();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(new string('=', 70));
        Console.WriteLine("  Semantic Kernel - Native Plugins Demo");
        Console.WriteLine(new string('=', 70));
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  What makes a plugin different from a helper method?");
        Console.WriteLine("    1. The Kernel is injected automatically - the plugin can call the LLM.");
        Console.WriteLine("    2. The Kernel knows the plugin exists and can expose it to the LLM");
        Console.WriteLine("       (auto function calling - next session).");
        Console.WriteLine();
        Console.WriteLine("  Demo flow:");
        Console.WriteLine("  [1] CheckFeasibility: C# calculates daily INR, LLM judges viability");
        Console.WriteLine("  [2] FEASIBLE -> generate itinerary  |  NOT_FEASIBLE -> tell user, exit");
        Console.WriteLine(new string('-', 70));
        Console.WriteLine();
    }

    private static int ReadTripDays()
    {
        while (true)
        {
            int d = ConsolePrompt.PositiveInt($"Number of days (1-{MaxTripDays})            : ");
            if (d <= MaxTripDays) return d;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Please enter a value between 1 and {MaxTripDays}.");
            Console.ResetColor();
        }
    }

    private static string ReadTravelStyle()
    {
        while (true)
        {
            string s = ConsolePrompt.Required(
                "Travel style (relaxed/adventure/culture/family/mixed): ").ToLowerInvariant();
            if (AllowedStyles.Contains(s, StringComparer.OrdinalIgnoreCase)) return s;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Choose one of: {string.Join(", ", AllowedStyles)}.");
            Console.ResetColor();
        }
    }

    private static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}

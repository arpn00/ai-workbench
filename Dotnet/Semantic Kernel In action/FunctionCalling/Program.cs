using Demo.Common;
using FunctionCalling.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using NativePlugins.Plugins;
using System.Diagnostics;

namespace FunctionCalling
{
    // ── Function invocation filter ────────────────────────────────────────────────
    // Intercepts every plugin call the LLM makes and prints it to the console.
    // This makes the autonomous chaining decisions visible during the demo.
    internal sealed class FunctionCallLogger : IFunctionInvocationFilter
    {
        public async Task OnFunctionInvocationAsync(
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, Task> next)
        {
            // ── Before call ───────────────────────────────────────────────────────
            string plugin = context.Function.PluginName ?? "?";
            string func   = context.Function.Name;

            // Build a compact argument list  e.g.  (amountInr: 150000, ...)
            var argParts = context.Arguments
                .Where(kv => kv.Value is not null)
                .Select(kv => $"{kv.Key}: {kv.Value}");
            string args = string.Join(", ", argParts);

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  [→ {plugin}.{func}({args})]");
            Console.ResetColor();

            // ── Execute ───────────────────────────────────────────────────────────
            await next(context);

            // ── After call ────────────────────────────────────────────────────────
            string? resultPreview = context.Result?.GetValue<string>();
            if (resultPreview is not null)
            {
                // Truncate long results for readability on screen
                if (resultPreview.Length > 120)
                    resultPreview = resultPreview[..120] + "…";

                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine(resultPreview);
                Console.ResetColor();
            }
            Console.WriteLine();
        }
    }

    internal class Program
    {
        private static readonly string[] AllowedStyles = ["relaxed", "adventure", "culture", "family", "mixed"];
        private const int MaxTripDays = 14;

        static async Task Main(string[] args)
        {
            Console.Title = "Semantic Kernel - Function Calling Demo";
            PrintBanner();

            AzureOpenAISettings settings = DemoConfiguration.LoadAzureOpenAISettings();
            if (!settings.IsConfigured)
            {
                WriteError("Azure OpenAI settings are missing.");
                Console.WriteLine("Set AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_API_KEY before running.");
                return;
            }

            var kernel = Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(
                    deploymentName: settings.DeploymentName,
                    endpoint: settings.Endpoint!,
                    apiKey: settings.ApiKey!)
                .Build();

            kernel.ImportPluginFromObject(new TravelPlannerPlugin(),     "TravelPlanner");
            kernel.ImportPluginFromObject(new CurrencyConverterPlugin(), "CurrencyConverter");

            kernel.FunctionInvocationFilters.Add(new FunctionCallLogger());

            // ── Show the tool manifest the LLM will receive ──────────────────────────
            //Console.ForegroundColor = ConsoleColor.Cyan;
            //Console.WriteLine("Functions visible to the LLM:");
            //Console.ResetColor();
            //foreach (var plugin in kernel.Plugins)
            //    foreach (var func in plugin)
            //        Console.WriteLine($"  {plugin.Name,-20} {func.Name,-25} {func.Description}");
            //Console.WriteLine(new string('-', 70));
            //Console.WriteLine();

            string destination = ConsolePrompt.Required("Destination (country or city) : ");
            int    days        = ReadTripDays();
            int    budgetInr   = ConsolePrompt.PositiveInt("Total budget in INR          : ");
            string style       = ReadTravelStyle();
            Console.WriteLine();


            const string travelPrompt = """
                You are an expert travel planning assistant with access to specialised tools.

                Trip request:
                - Destination : {{$destination}}
                - Duration    : {{$days}} days
                - Total budget: INR {{$budgetInr}}
                - Travel style: {{$style}}

                You MUST call the available tools in this exact sequence:
                1. Call CurrencyConverter.ConvertToDestinationCurrency with destination and budgetInr.
                2. Call TravelPlanner.CheckFeasibility with destination, days, budgetInr,
                   and pass the exact converter output into convertedBudgetContext.

                Output rules:
                - First print the exact converter output on one line.
                - Then print the exact planner feasibility verdict on one line.

                Reuse this itinerary policy from the NativePlugins demo:
                If verdict starts with FEASIBLE:
                - Write a concise day-by-day itinerary that fits within this budget.

                If verdict starts with NOT_FEASIBLE:
                - Do NOT generate an itinerary.
                - Start with: "Not feasible in this budget."
                - Then provide exactly 3 short bullet points explaining why and how much to increase budget.
                """;

            var executionSettings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };


            var travelFunction = kernel.CreateFunctionFromPrompt(
                travelPrompt,
                executionSettings: executionSettings);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[Auto Function Calling] Invoking prompt — the LLM will now autonomously");
            Console.WriteLine("  decide which functions to call, with what args, and in what order.");
            Console.ResetColor();
            Console.WriteLine();

            var timer = Stopwatch.StartNew();
            FunctionResult response;
            try
            {
                response = await kernel.InvokeAsync(travelFunction, new KernelArguments
                {
                    ["destination"] = destination,
                    ["days"]        = days,
                    ["budgetInr"]   = budgetInr,
                    ["style"]       = style,
                });
            }
            catch (Exception ex)
            {
                WriteError("Auto function calling chain failed.");
                Console.WriteLine($"  Reason : {ex.Message}");
                return;
            }
            finally
            {
                timer.Stop();
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== Travel Planning Summary ===");
            Console.ResetColor();
            Console.WriteLine(new string('-', 70));
            Console.WriteLine(response.GetValue<string>());
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
            Console.WriteLine("  Semantic Kernel - Function Calling Demo");
            Console.WriteLine(new string('=', 70));
            Console.ResetColor();
            Console.WriteLine();
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
}

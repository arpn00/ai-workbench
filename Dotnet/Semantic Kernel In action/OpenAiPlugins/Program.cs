using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using Demo.Common;
using System.Diagnostics;
using System.Text.Json;

namespace OpenAiPlugins
{
    internal class Program
    {
        private static readonly string[] AllowedStyles = ["relaxed", "adventure", "culture", "family", "mixed"];
        private const int MaxTripDays = 14;

        static async Task Main(string[] args)
        {
            Console.Title = "Semantic Kernel Plugins Demo";
            PrintBanner();

            AzureOpenAISettings settings = DemoConfiguration.LoadAzureOpenAISettings();
            if (!settings.IsConfigured)
            {
                WriteError("Azure OpenAI settings are missing.");
                Console.WriteLine("Set AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_API_KEY before running the demo.");
                Console.WriteLine("Optional: AZURE_OPENAI_DEPLOYMENT (defaults to gpt-4o-mini).");
                return;
            }


            #region Kernel
            var builder = Kernel.CreateBuilder();

            builder.AddAzureOpenAIChatClient(
                deploymentName: settings.DeploymentName,
                endpoint: settings.Endpoint!,
                apiKey: settings.ApiKey!
            );

            var kernel = builder.Build();

            #endregion

            #region Plugins
            string pluginsRoot = FindPluginsRoot();
            string schemaPath = Path.Combine(pluginsRoot, "schema", "travelSchema.json");

            // Switch this flag to compare classic (config.json + skprompt.txt) and YAML plugin formats.
            bool useYamlPlugin = args.Any(a => string.Equals(a, "--yaml", StringComparison.OrdinalIgnoreCase));
            string pluginName = useYamlPlugin ? "TravelSchemaYaml" : "TravelSchemaClassic";
            string pluginPath = Path.Combine(pluginsRoot, pluginName);

            Console.WriteLine($"Plugin format: {(useYamlPlugin ? "YAML (prompt.yaml)" : "Classic (config.json + skprompt.txt)")}");

            if (!Directory.Exists(pluginPath))
            {
                WriteError($"Plugin directory not found: {pluginPath}");
                return;
            }

            if (!File.Exists(schemaPath))
            {
                WriteError($"Schema file not found: {schemaPath}");
                return;
            }

            string schemaJson;
            try
            {
                schemaJson = File.ReadAllText(schemaPath);
                JsonDocument.Parse(schemaJson);
                Console.WriteLine($"Schema loaded from: {schemaPath}");
            }
            catch (Exception ex)
            {
                WriteError($"Schema load failed: {ex.Message}");
                return;
            }

            var schemaPlugin = kernel.ImportPluginFromPromptDirectory(pluginPath, pluginName);
            Console.WriteLine($"Plugin imported: {pluginName}");

            #endregion

            // --- USER INPUT ---
            string destination = ConsolePrompt.Required("Enter destination country/city: ");
            int daysCount = ReadTripDays();
            string budget = ConsolePrompt.Required("Budget (e.g., INR 15000): ");
            string style = ReadTravelStyle();

            #region Prompting
            string fullPrompt = @"You are an expert travel planner.
Create realistic, budget-aware, and well-structured itineraries.
Keep the response concise, practical, and useful for real travelers.

Trip Request:
- Destination: {{$destination}}
- Duration: {{$days}} days
- Budget: {{$budget}}
- Travel style: {{$style}}

Schema Constraints:
{{$schemaGuidance}}

How to use the schema constraints:
- Treat the schema constraints as a strong planning preference, not a decorative note.
- Let them materially influence activity selection, trip pacing, cost realism, and indoor or outdoor choices.
- If a generic travel recommendation conflicts with the schema constraints, prefer the schema constraints.
- Keep the itinerary feasible for the stated budget, duration, and travel style.
- If bad weather is likely to affect an outdoor activity, replace it with a relevant indoor alternative from the schema guidance.

Instructions:
- Provide a day-wise itinerary.
- Suggest key places to visit.
- Include approximate costs for major activities or daily spend.
- Avoid unrealistic travel jumps, overcrowded days, and premium recommendations for a low budget.
- Make the travel style visible in the recommendations.
- End with a short Constraint Notes section with 2 bullet points explaining how the itinerary follows the schema constraints.

Output Format:
Day 1:
- Activities

Day 2:
- Activities

Constraint Notes:
- <how the plan follows budget or feasibility guidance>
- <how the plan follows weather or style guidance>";


            var arguments = new KernelArguments
            {
                ["destination"] = destination,
                ["days"] = daysCount,
                ["budget"] = budget,
                ["style"] = style,
                ["schemaJson"] = schemaJson,
            };

            Console.WriteLine("\nGenerating schema guidance...\n");
            var guidanceTimer = Stopwatch.StartNew();

            string schemaGuidance;
            try
            {
                var guidanceResult = await kernel.InvokeAsync(schemaPlugin["GetSchemaGuidance"], arguments);
                schemaGuidance = guidanceResult.GetValue<string>()?.Trim() ?? string.Empty;
            }
            catch (Exception ex)
            {
                WriteError($"Failed to generate schema guidance: {ex.Message}");
                return;
            }
            finally
            {
                guidanceTimer.Stop();
            }

            if (!LooksLikeStructuredGuidance(schemaGuidance))
            {
                schemaGuidance = @"Budget Band:
- Use a moderate budget interpretation unless the user clearly states luxury or shoestring spending.
Trip Feasibility:
- Keep the plan realistic for the number of days requested.
Required Rules:
- Match activities to the requested travel style.
- Keep daily pacing realistic and avoid too many distant stops in one day.
- Prefer budget-aware food, transport, and sightseeing suggestions.
Bad Weather Fallbacks:
- Replace outdoor-heavy activities with museums, cultural sites, food experiences, or local indoor attractions.
- Keep at least one indoor backup option for each day.";
                Console.WriteLine("Schema guidance fallback applied because the plugin output was incomplete.");
            }

            arguments["schemaGuidance"] = schemaGuidance;
            Console.WriteLine(schemaGuidance);
            #endregion

            var travelFunction = kernel.CreateFunctionFromPrompt(
            fullPrompt,
            functionName: "GeneratePlan"
        );
            Console.WriteLine("\nGenerating itinerary...\n");
            var timer = Stopwatch.StartNew();

            FunctionResult result;
            try
            {
                result = await kernel.InvokeAsync(travelFunction, arguments);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to generate itinerary.");
                Console.ResetColor();
                Console.WriteLine($"Reason: {ex.Message}");
                Console.WriteLine("Tip: verify endpoint, key, deployment name, plugin files, and network access.");
                return;
            }
            finally
            {
                timer.Stop();
            }


            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== Travel Plan ===");
            Console.ResetColor();
            Console.WriteLine(new string('-', 70));
            Console.WriteLine(result.GetValue<string>());
            Console.WriteLine(new string('-', 70));
            Console.WriteLine($"Completed in {timer.ElapsedMilliseconds} ms.");

            Console.ReadKey();
        }

        private static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Semantic Kernel Plugins Demo");
            Console.ResetColor();
            Console.WriteLine(new string('=', 70));
        }

        private static string FindPluginsRoot()
        {
            string? current = AppContext.BaseDirectory;

            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, "PluginsFolder");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            throw new DirectoryNotFoundException("Could not locate PluginsFolder from the current application base directory.");
        }

        private static int ReadTripDays()
        {
            while (true)
            {
                int days = ConsolePrompt.PositiveInt($"Number of days (1-{MaxTripDays}): ");
                if (days <= MaxTripDays)
                {
                    return days;
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Please keep the trip length between 1 and {MaxTripDays} days for this demo schema.");
                Console.ResetColor();
            }
        }

        private static string ReadTravelStyle()
        {
            while (true)
            {
                string style = ConsolePrompt.Required("Travel style (relaxed, adventure, culture, family, mixed): ").ToLowerInvariant();
                if (AllowedStyles.Contains(style, StringComparer.OrdinalIgnoreCase))
                {
                    return style;
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Choose one of: {string.Join(", ", AllowedStyles)}.");
                Console.ResetColor();
            }
        }

        private static bool LooksLikeStructuredGuidance(string schemaGuidance)
        {
            return !string.IsNullOrWhiteSpace(schemaGuidance)
                && schemaGuidance.Contains("Budget Band:", StringComparison.OrdinalIgnoreCase)
                && schemaGuidance.Contains("Trip Feasibility:", StringComparison.OrdinalIgnoreCase)
                && schemaGuidance.Contains("Required Rules:", StringComparison.OrdinalIgnoreCase)
                && schemaGuidance.Contains("Bad Weather Fallbacks:", StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}

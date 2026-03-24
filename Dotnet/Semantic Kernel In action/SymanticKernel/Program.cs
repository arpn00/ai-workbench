using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics;
using Demo.Common;

namespace SymanticKernel
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "Semantic Kernel Prompt Demo";
            PrintBanner();

            AzureOpenAISettings settings = DemoConfiguration.LoadAzureOpenAISettings();

            #region Kernel
            var builder = Kernel.CreateBuilder();

            builder.AddAzureOpenAIChatClient(
                deploymentName: settings.DeploymentName,
                endpoint: settings.Endpoint!,
                apiKey: settings.ApiKey!
            );

            var kernel = builder.Build();

            #endregion

            // --- USER INPUT ---
            string destination = ConsolePrompt.Required("Enter destination country/city: ");
            int daysCount = ConsolePrompt.PositiveInt("Number of days: ");
            string budget = ConsolePrompt.Required("Budget (e.g., INR 15000): ");
            string style = ConsolePrompt.Required("Travel style (e.g., relaxed, adventure): ");

            #region Prompting
            string persona = @"You are an expert travel planner.
Create realistic, budget-aware, practical itineraries.
Keep the response concise and useful for real travelers.";

            string task = @"Create a travel plan with the following details:

                           Destination: {{$destination}}
                           Duration: {{$days}} days
                           Budget: {{$budget}}
                           Travel Style: {{$style}}

                        Instructions:
                            - Provide a day-wise itinerary
                            - Suggest key places to visit
                            - Include approximate costs
                            - Avoid unrealistic recommendations

                        Output Format:
                            Day 1:
                            - Activities

                            Day 2:
                           - Activities
";

            string fullPrompt = persona + "\n\n" + task;

            var arguments = new KernelArguments
            {
                ["destination"] = destination,
                ["days"] = daysCount,
                ["budget"] = budget,
                ["style"] = style
            };
            #endregion



            #region FunctionFromPrompt
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
                Console.WriteLine("Tip: verify endpoint, key, deployment name, and network access.");
                return;
            }
            finally
            {
                timer.Stop();
            }
            #endregion



            //var result = await kernel.InvokePromptAsync(fullPrompt, arguments);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== Travel Plan ===");
            Console.ResetColor();
            Console.WriteLine(new string('-', 70));
            Console.WriteLine(result.GetValue<string>());
            Console.WriteLine(new string('-', 70));

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        private static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Semantic Kernel Prompting Demo");
            Console.ResetColor();
            Console.WriteLine(new string('=', 70));
        }
    }
}

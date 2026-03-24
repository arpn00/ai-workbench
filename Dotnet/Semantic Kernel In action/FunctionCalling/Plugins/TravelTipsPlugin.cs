using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FunctionCalling.Plugins;

/// <summary>
/// Prompt-based (semantic) plugin — mirrors TravelPlannerPlugin's pattern of
/// receiving an auto-injected Kernel and making an internal LLM call.
///
/// Demonstrates that plugins can wrap LLM prompts just like they wrap C# logic.
/// The Kernel is injected automatically by Semantic Kernel; no manual wiring needed.
/// </summary>
public sealed class TravelTipsPlugin
{
    [KernelFunction]
    [Description(
        "Returns practical travel tips for a destination: what to pack, local customs, " +
        "safety advice, and transport options. Call this after feasibility is confirmed.")]
    public async Task<string> GetTravelTipsAsync(
        [Description("Destination country or city, e.g. 'Paris' or 'Goa'")]
        string destination,

        [Description("Travel style: relaxed, adventure, culture, family, or mixed")]
        string style,

        // SK injects the Kernel automatically — allows us to call the LLM from inside the plugin.
        Kernel kernel)
    {
        const string tipsPrompt = """
            You are a seasoned travel advisor.

            Destination : {{$destination}}
            Travel style: {{$style}}

            Provide exactly 4 concise bullet points covering:
            1. Top 2 items to pack for this destination and style
            2. One key local custom or etiquette tip
            3. One safety or health tip
            4. Best local transport option

            Keep each bullet to one sentence. No intro or closing line.
            """;

        var result = await kernel.InvokeAsync(
            kernel.CreateFunctionFromPrompt(tipsPrompt),
            new KernelArguments
            {
                ["destination"] = destination,
                ["style"]       = style,
            });

        return result.GetValue<string>()?.Trim()
            ?? "No tips available at this time.";
    }
}

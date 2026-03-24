using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FunctionCalling.Plugins;

/// <summary>
/// LLM-powered plugin that converts an INR budget to the primary local currency
/// of the destination. The model chooses the target currency and provides a
/// concise conversion line that can be passed to other plugins.
/// </summary>
public sealed class CurrencyConverterPlugin
{
    [KernelFunction]
    [Description(
        "Converts an INR (Indian Rupee) amount to the destination's most relevant local currency. " +
        "Returns one concise line that includes currency code and converted amount, e.g. " +
        "'INR 150,000 ≈ EUR 1,650'.")]
    public async Task<string> ConvertToDestinationCurrencyAsync(
        [Description("Destination country or city, e.g. 'Paris' or 'Tokyo'")]
        string destination,

        [Description("Amount in INR to convert, e.g. 150000")]
        int amountInr,

        // SK injects the Kernel automatically so this plugin can perform an LLM call.
        Kernel kernel)
    {
        const string conversionPrompt = """
            Convert INR {{$amountInr}} to the most relevant local currency for {{$destination}}.

            Rules:
            - Choose one best-fit local currency only.
            - Use a realistic current exchange estimate.
            - Return EXACTLY one line in this format:
              INR <inr_amount_with_commas> ≈ <CURRENCY_CODE> <converted_amount>
            - Example: INR 150,000 ≈ EUR 1,650
            - No extra text.
            """;

        var result = await kernel.InvokeAsync(
            kernel.CreateFunctionFromPrompt(conversionPrompt),
            new KernelArguments
            {
                ["destination"] = destination,
                ["amountInr"]   = amountInr,
            });

        return result.GetValue<string>()?.Trim()
            ?? $"INR {amountInr:N0} ≈ USD 0";
    }
}

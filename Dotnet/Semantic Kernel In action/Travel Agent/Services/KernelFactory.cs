using Demo.Common;
using FunctionCalling.Plugins;
using Microsoft.SemanticKernel;
using NativePlugins.Plugins;

namespace Travel_Agent.Services;

public static class KernelFactory
{
    public static Kernel CreateKernel(AzureOpenAISettings settings)
    {
        Kernel kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(
                deploymentName: settings.DeploymentName,
                endpoint: settings.Endpoint!,
                apiKey: settings.ApiKey!)
            .Build();

        kernel.FunctionInvocationFilters.Add(new FunctionCallTraceFilter());

        kernel.ImportPluginFromObject(new CurrencyConverterPlugin(), "CurrencyConverter");
        kernel.ImportPluginFromObject(new TravelPlannerPlugin(), "TravelPlanner");

        return kernel;
    }
}

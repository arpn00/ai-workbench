using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace MCPServerIntegration;

public static class McpKernelToolRegistrar
{
    public const string PluginName = "McpFilesystemTools";

    /// <summary>
    /// Manual plugin wrappers are useful for curated and business-safe functions.
    /// Dynamic MCP registration is useful when you want the MCP server itself to behave like
    /// a Semantic Kernel plugin without manually mapping every tool.
    /// </summary>
    public static async Task<List<Tool>> RegisterMcpToolsAsKernelFunctions(
        IKernelBuilderPlugins plugins,
        MCPFilesystemClient mcpClient,
        ILogger logger,
        string pluginName = PluginName)
    {
        var tools = await mcpClient.ListToolsAsync();
        var functions = new List<KernelFunction>();

        foreach (var tool in tools)
        {
            var toolName = tool.Name;
            var function = KernelFunctionFactory.CreateFromMethod(
                method: async (KernelArguments arguments, CancellationToken _) =>
                {
                    var toolArgs = new Dictionary<string, object>();
                    foreach (var argument in arguments)
                    {
                        if (argument.Value is not null)
                        {
                            toolArgs[argument.Key] = argument.Value;
                        }
                    }

                    return await mcpClient.CallToolAsync(toolName, toolArgs);
                },
                functionName: toolName,
                description: tool.Description);

            functions.Add(function);
        }

        plugins.Add(KernelPluginFactory.CreateFromFunctions(pluginName, functions));
        logger.LogInformation("Registered {Count} MCP tools as kernel functions in plugin {PluginName}", functions.Count, pluginName);

        return tools;
    }
}

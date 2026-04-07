using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics;
using MCPServerIntegration;

// Configure logging
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("MCPServerIntegration");

// Load configuration from appsettings.json; code already falls back to env vars where needed
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .Build();

try
{
    logger.LogInformation("=== MCP Filesystem Server Integration with Semantic Kernel ===");
    logger.LogInformation("Using the official @modelcontextprotocol/server-filesystem npm package\n");
    
    // Create demo directory for testing
    string demoDir = Path.Combine(Path.GetTempPath(), "MCPServerDemo");
    Directory.CreateDirectory(demoDir);
    logger.LogInformation($"✓ Created demo directory: {demoDir}");

    // Create sample files
    File.WriteAllText(Path.Combine(demoDir, "readme.txt"), "This is a README file\nCreated: " + DateTime.Now);
    File.WriteAllText(Path.Combine(demoDir, "config.json"), """
    {
      "application": "MCPServerIntegration",
      "version": "1.0",
      "features": ["mcp-integration", "kernel-tools"]
    }
    """);
    File.WriteAllText(Path.Combine(demoDir, "notes.txt"), "MCP Integration: Using official server\nStatus: Testing");
    logger.LogInformation("✓ Created sample files\n");

    // Initialize the MCP filesystem client (starts the actual MCP server)
    logger.LogInformation("--- Initializing MCP Filesystem Server ---");
    using var mcpClient = new MCPFilesystemClient(demoDir);
    await mcpClient.InitializeAsync();
    logger.LogInformation("✓ MCP Server connected via JSON-RPC 2.0\n");

    // Manual wrappers are best when you want curated, strongly typed, business-safe functions.
    await RunManualPluginDemoAsync(mcpClient, logger, demoDir);

    // Dynamic registration is best when you want MCP tools to behave like a full kernel plugin
    // without manually mapping every tool one-by-one.
    await RunDynamicRegistrationDemoAsync(mcpClient, logger, demoDir);
    await RunAutoFunctionCallingDemoAsync(mcpClient, logger, demoDir, configuration);

    // Cleanup
    Directory.Delete(demoDir, recursive: true);
    logger.LogInformation("✓ Cleaned up demo directory");
    logger.LogInformation("✓ MCP Server connection closed");
}
catch (Exception ex)
{
    logger.LogError($"Error: {ex.Message}");
    logger.LogError($"StackTrace: {ex.StackTrace}");
}

static async Task RunManualPluginDemoAsync(MCPFilesystemClient mcpClient, ILogger logger, string demoDir)
{
    logger.LogInformation("--- Approach 1: Manual Curated Plugin ---");

    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.Plugins.AddFromObject(new FileSystemPlugin(mcpClient, logger), nameof(FileSystemPlugin));
    var kernel = kernelBuilder.Build();

    var readFileFunction = kernel.Plugins[nameof(FileSystemPlugin)]["ReadFile"];
    var readResult = await kernel.InvokeAsync(readFileFunction, new() { { "path", Path.Combine(demoDir, "readme.txt") } });
    logger.LogInformation("Manual ReadFile result:\n{Result}\n", readResult);
}

static async Task RunDynamicRegistrationDemoAsync(MCPFilesystemClient mcpClient, ILogger logger, string demoDir)
{
    logger.LogInformation("--- Approach 2: Dynamic MCP Tool Registration ---");

    var dynamicKernelBuilder = Kernel.CreateBuilder();
    var tools = await McpKernelToolRegistrar.RegisterMcpToolsAsKernelFunctions(dynamicKernelBuilder.Plugins, mcpClient, logger);
    var dynamicKernel = dynamicKernelBuilder.Build();

    logger.LogInformation("Discovered MCP tools:");
    foreach (var tool in tools)
    {
        logger.LogInformation("• {ToolName}: {ToolDescription}", tool.Name, tool.Description);
    }

    // Demonstrate invoking a dynamically registered MCP function through the kernel.
    var listDirectory = dynamicKernel.Plugins[McpKernelToolRegistrar.PluginName]["list_directory"];
    var listResult = await dynamicKernel.InvokeAsync(listDirectory, new() { { "path", demoDir } });
    logger.LogInformation("Dynamic list_directory result:\n{Result}\n", listResult);
}

static async Task RunAutoFunctionCallingDemoAsync(MCPFilesystemClient mcpClient, ILogger logger, string demoDir, IConfiguration configuration)
{
    logger.LogInformation("--- Auto Function Calling with Dynamic MCP Tools ---");

    // Read from appsettings.json (OpenAI section), with environment variables as fallback
    var openAiApiKey = configuration["AzureOpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    var openAiModel = configuration["OpenAI:Model"] ?? Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";

    if (string.IsNullOrWhiteSpace(openAiApiKey))
    {
        logger.LogInformation("Skipping auto-calling demo because OpenAI:ApiKey is not set in appsettings.json or OPENAI_API_KEY env var.");
        logger.LogInformation("Set OpenAI.ApiKey in appsettings.json (or OPENAI_API_KEY env var) to run this section.\n");
        return;
    }

    var llmKernelBuilder = Kernel.CreateBuilder();
    llmKernelBuilder.AddOpenAIChatCompletion(openAiModel, openAiApiKey);
    await McpKernelToolRegistrar.RegisterMcpToolsAsKernelFunctions(llmKernelBuilder.Plugins, mcpClient, logger);
    var llmKernel = llmKernelBuilder.Build();

    var executionSettings = new OpenAIPromptExecutionSettings
    {
        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
    };

    var userRequest = $"List all directory contents under '{demoDir}'";
    var autoPrompt = $$"""
    You are a filesystem assistant.
    The user asked: {{userRequest}}

    Choose and call the right MCP filesystem tools.
    Return only a short bullet list of files and folders.
    """;

    var autoResult = await llmKernel.InvokePromptAsync(
        autoPrompt,
        new KernelArguments(executionSettings));

    logger.LogInformation("User Request: {UserRequest}", userRequest);
    logger.LogInformation("LLM Response (dynamic tools auto-invoked):\n{Result}\n", autoResult);
}

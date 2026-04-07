using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace MCPServerIntegration;

/// <summary>
/// Semantic Kernel Plugin that exposes MCP filesystem server operations as kernel functions.
/// This uses the official @modelcontextprotocol/server-filesystem via JSON-RPC 2.0.
/// </summary>
public class FileSystemPlugin
{
    private readonly MCPFilesystemClient _mcpClient;
    private readonly ILogger _logger;

    public FileSystemPlugin(MCPFilesystemClient mcpClient, ILogger logger)
    {
        _mcpClient = mcpClient;
        _logger = logger;
    }

    // Manual wrapper approach: keep curated, business-friendly functions instead of exposing every MCP tool directly.
    [KernelFunction]
    [Description("Reads the content of a file. This uses the MCP filesystem server.")]
    public async Task<string> ReadFile(
        [Description("The path to the file to read")] string path)
    {
        try
        {
            var result = await _mcpClient.CallToolAsync("read_file", new() { { "path", path } });
            _logger.LogInformation($"✓ MCP read_file: {Path.GetFileName(path)}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error reading file {path}: {ex.Message}");
            return $"ERROR: {ex.Message}";
        }
    }
}


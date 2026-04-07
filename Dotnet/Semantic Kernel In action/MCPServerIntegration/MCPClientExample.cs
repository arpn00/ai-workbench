using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCPServerIntegration;

/// <summary>
/// MCP Client that communicates with the official @modelcontextprotocol/server-filesystem
/// npm package via JSON-RPC 2.0 over stdio.
/// 
/// This client starts the MCP server as a subprocess and communicates with it using
/// the Model Context Protocol specification.
/// </summary>
public class MCPFilesystemClient : IDisposable
{
    private readonly Process? _serverProcess;
    private readonly StreamWriter? _stdin;
    private readonly StreamReader? _stdout;
    private readonly StreamReader? _stderr;
    private readonly Dictionary<int, TaskCompletionSource<JsonElement>> _pendingRequests;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private int _requestId = 1;
    private bool _isInitialized = false;

    public MCPFilesystemClient(string? toolsDirectory = null)
    {
        _pendingRequests = new();
        _cancellationTokenSource = new();
        
        try
        {
            // Use temp directory if none specified
            string workingDirectory = toolsDirectory ?? Path.GetTempPath();
            
            // Start the official MCP filesystem server
            _serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c npx @modelcontextprotocol/server-filesystem \"{workingDirectory}\"",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            if (_serverProcess.Start())
            {
                _stdin = _serverProcess.StandardInput;
                _stdout = _serverProcess.StandardOutput;
                _stderr = _serverProcess.StandardError;
                
                // Give server time to start up
                System.Threading.Thread.Sleep(500);
                
                // Start reading responses from the server
                _ = ReadResponsesAsync(_cancellationTokenSource.Token);
                
                _isInitialized = true;
            }
            else
            {
                throw new InvalidOperationException("Failed to start MCP server process");
            }
        }
        catch (FileNotFoundException ex) when (ex.Message.Contains("npx"))
        {
            throw new InvalidOperationException(
                "npx not found. Ensure Node.js is installed. " +
                "Download from: https://nodejs.org/",
                ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to start MCP filesystem server. Ensure Node.js is installed and " +
                "@modelcontextprotocol/server-filesystem is available via npm.\n" +
                "Install with: npm install -g @modelcontextprotocol/server-filesystem\n" +
                $"Error: {ex.GetType().Name}: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Initialize the MCP connection (MCP initialize handshake)
    /// </summary>
    public async Task InitializeAsync()
    {
        if (!_isInitialized || _stdin == null)
            throw new InvalidOperationException("Server not initialized");

        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = _requestId++,
            Method = "initialize",
            Params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new
                {
                    name = "MCPServerIntegration",
                    version = "1.0"
                }
            }
        };

        await SendRequestAsync(request);
    }

    /// <summary>
    /// Get available resources from the filesystem server
    /// </summary>
    public async Task<List<Resource>> ListResourcesAsync()
    {
        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = _requestId++,
            Method = "resources/list"
        };

        var response = await SendRequestAsync(request);
        
        // Parse resources from response
        var resources = new List<Resource>();
        if (response.TryGetProperty("result", out var result) && 
            result.TryGetProperty("resources", out var resourcesArray))
        {
            foreach (var resource in resourcesArray.EnumerateArray())
            {
                resources.Add(new Resource
                {
                    Uri = resource.GetProperty("uri").GetString() ?? "",
                    Name = resource.GetProperty("name").GetString() ?? "",
                    MimeType = resource.TryGetProperty("mimeType", out var mime) 
                        ? mime.GetString() ?? "text/plain" 
                        : "text/plain"
                });
            }
        }
        
        return resources;
    }

    /// <summary>
    /// List available tools from the MCP server
    /// </summary>
    public async Task<List<Tool>> ListToolsAsync()
    {
        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = _requestId++,
            Method = "tools/list"
        };

        var response = await SendRequestAsync(request);
        
        var tools = new List<Tool>();
        if (response.TryGetProperty("result", out var result) && 
            result.TryGetProperty("tools", out var toolsArray))
        {
            foreach (var tool in toolsArray.EnumerateArray())
            {
                tools.Add(new Tool
                {
                    Name = tool.GetProperty("name").GetString() ?? "",
                    Description = tool.TryGetProperty("description", out var desc)
                        ? desc.GetString() ?? ""
                        : "",
                    InputSchema = tool.TryGetProperty("inputSchema", out var schema)
                        ? schema.GetRawText()
                        : "{}"
                });
            }
        }
        
        return tools;
    }

    /// <summary>
    /// Call a tool on the MCP server
    /// </summary>
    public async Task<string> CallToolAsync(string toolName, Dictionary<string, object> arguments)
    {
        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = _requestId++,
            Method = "tools/call",
            Params = new
            {
                name = toolName,
                arguments = arguments
            }
        };

        var response = await SendRequestAsync(request);
        
        if (response.TryGetProperty("result", out var result))
        {
            if (result.TryGetProperty("content", out var content) && 
                content.ValueKind == JsonValueKind.Array)
            {
                var firstContent = content.EnumerateArray().FirstOrDefault();
                if (firstContent.TryGetProperty("text", out var text))
                {
                    return text.GetString() ?? "";
                }
            }
        }

        return response.GetRawText();
    }

    /// <summary>
    /// Send a JSON-RPC request to the server and wait for response
    /// </summary>
    private async Task<JsonElement> SendRequestAsync(JsonRpcRequest request)
    {
        if (_stdin == null || _stdout == null)
            throw new InvalidOperationException("Server not properly initialized");

        var tcs = new TaskCompletionSource<JsonElement>();
        var requestId = request.Id;
        
        _pendingRequests[requestId] = tcs;

        try
        {
            var json = JsonSerializer.Serialize(request);
            await _stdin.WriteLineAsync(json);
            await _stdin.FlushAsync();
            
            // Wait for response with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _pendingRequests.Remove(requestId);
        }
    }

    /// <summary>
    /// Read responses from the MCP server
    /// </summary>
    private async Task ReadResponsesAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_stdout == null)
                return;

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _stdout.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(line))
                    continue;

                try
                {
                    var response = JsonDocument.Parse(line).RootElement;
                    
                    if (response.TryGetProperty("id", out var idElement) && 
                        idElement.TryGetInt32(out var id))
                    {
                        if (_pendingRequests.TryGetValue(id, out var tcs))
                        {
                            tcs.SetResult(response);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing response: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading from server: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _stdin?.Dispose();
        _stdout?.Dispose();
        _stderr?.Dispose();
        _serverProcess?.Dispose();
    }
}

/// <summary>
/// JSON-RPC 2.0 Request
/// </summary>
public class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; set; }
}

/// <summary>
/// MCP Resource
/// </summary>
public class Resource
{
    public string Uri { get; set; } = "";
    public string Name { get; set; } = "";
    public string MimeType { get; set; } = "text/plain";
}

/// <summary>
/// MCP Tool
/// </summary>
public class Tool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string InputSchema { get; set; } = "{}";
}


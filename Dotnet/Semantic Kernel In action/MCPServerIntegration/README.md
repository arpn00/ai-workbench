# MCP Server Integration with Semantic Kernel

This project demonstrates how to integrate the official **Model Context Protocol (MCP) Filesystem Server** with a .NET application using **Semantic Kernel**, allowing the AI kernel to use MCP operations as actual tools/functions.

## Overview

The key innovation here is that **MCP filesystem operations become Semantic Kernel tools**, meaning:
- The kernel can directly invoke MCP operations
- An LLM can be instructed to use MCP tools autonomously
- MCP operations are properly registered as kernel plugins with descriptions
- Tools can be chained together for complex workflows

## Architecture

```
This project demonstrates **real integration** with the official **Model Context Protocol (MCP) Filesystem Server** from [@modelcontextprotocol](https://modelcontextprotocol.io), using JSON-RPC 2.0 communication over stdio. The MCP server runs as a subprocess, and its tools are exposed as **Semantic Kernel plugins**.
│   Prompt    │
│  (via LLM)  │
└──────┬──────┘
       │
│  │ FileSystemPlugin   │  │
│  │ - ReadFile         │  │
│  │ - ListDirectory    │  │
│  │ - SearchFiles      │  │
│  │ - GetFileInfo      │  │
│  └────────────────────┘  │
└──────────┬───────────────┘
           │
           ▼
┌──────────────────────────┐
│  MCP Filesystem Client   │
│  (MCPFilesystemWrapper)  │
└──────────┬───────────────┘
           │
           ▼
   ┌───────────────┐
   │ File System   │
   │ (Sandboxed)   │
   └───────────────┘
```

## Prerequisites

1. **.NET 8.0+** - Download from https://dotnet.microsoft.com/download
2. **Node.js 18+** - Required for running the MCP filesystem server (https://nodejs.org)
3. **Semantic Kernel** - Referenced in the project file

## Installation

### 1. Install the MCP Filesystem Server

The MCP server enforces security at the server level:
- Only directories passed to the server on startup are accessible
- All paths are validated server-side
- No directory traversal attacks possible

## Implementation Details

### How It Works
### 2. Build the Project
1. **Process Management**
   - MCPFilesystemClient starts `npx @modelcontextprotocol/server-filesystem` as subprocess
   - Communication happens via stdin/stdout
   - Process is properly cleaned up on disposal

2. **JSON-RPC 2.0 Communication**
   ```csharp
   // Send request
   var request = new JsonRpcRequest 
   { 
       JsonRpc = "2.0",
       Id = 1,
       Method = "tools/call",
       Params = new { name = "read_file", arguments = new { path = filePath } }
   };
   
   // Receive response asynchronously
   var response = await SendRequestAsync(request);
   ```
```bash
3. **Tool Invocation Through Kernel**
   ```csharp
   // Get the MCP tool from kernel
   var readFunc = kernel.Plugins[nameof(FileSystemPlugin)]["ReadFile"];
   
   // Invoke through kernel with arguments
   var result = await kernel.InvokeAsync(readFunc, 
       new() { { "path", "/path/to/file.txt" } });
   ```
dotnet build MCPServerIntegration.sln
### Adding More MCP Tools to the Plugin
```
To expose additional MCP tools through the kernel:

```csharp
[KernelFunction]
[Description("Creates a new file with the specified content")]
public async Task<string> WriteFile(
    [Description("The path where to create the file")] string path,
    [Description("The content to write to the file")] string content)
{
    return await _mcpClient.CallToolAsync("write_file", 
        new() { { "path", path }, { "content", content } });
}
```
### 3. Run the Sample

```bash
dotnet run
```
The official MCP server supports write operations:

```csharp
// Add these methods to FileSystemPlugin:
[KernelFunction]
[Description("Write content to a file")]
public async Task<string> WriteFile(string path, string content)
{
    try
    {
        var result = await _mcpClient.CallToolAsync("write_file", 
            new() { { "path", path }, { "content", content } });
        _logger.LogInformation($"✓ MCP write_file: {Path.GetFileName(path)}");
        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error writing file {path}: {ex.Message}");
        return $"ERROR: {ex.Message}";
    }
}
```
## Key Components
### Search and Pattern Matching

Use glob patterns for powerful file searching:
### 1. MCPFilesystemClient
```csharp
[KernelFunction]
[Description("Search for files matching a glob pattern")]
public async Task<string> SearchFiles(string pattern)
{
    // MCP supports patterns like: *.txt, **/*.json, src/**/*.cs
    return await _mcpClient.CallToolAsync("search_files", 
        new() { { "pattern", pattern } });
}
```
Communicates with the official MCP filesystem server via JSON-RPC 2.0:
### Working with Media Files
- Starts the `@modelcontextprotocol/server-filesystem` npm package as subprocess
Read images and audio files as base64:
- Handles JSON-RPC 2.0 request/response communication
```csharp
[KernelFunction]
[Description("Read a media file (image or audio)")]
public async Task<string> ReadMediaFile(string path)
{
    // Returns base64 encoded content
    return await _mcpClient.CallToolAsync("read_media_file", 
        new() { { "path", path } });
}
```
- Parses MCP tool definitions and results
### Editing Files
- Methods: `InitializeAsync()`, `ListToolsAsync()`, `CallToolAsync()`
Make line-based edits without replacing entire files:

```csharp
[KernelFunction]
[Description("Edit specific lines in a file")]
public async Task<string> EditFile(string path, int line, string content)
{
    return await _mcpClient.CallToolAsync("edit_file", 
        new() { 
            { "path", path },
            { "newContent", content },
            { "lineNumber", line }
        });
}
```
**Available MCP Tools:**
- `read_file` / `read_text_file` - Read file contents
- `read_media_file` - Read images and audio files
- `read_multiple_files` - Read multiple files at once
- `write_file` - Create or overwrite files
- `edit_file` - Make line-based edits
- `create_directory` - Create directories
- `list_directory` - List directory contents
- `search_files` - Search for files by pattern
- `get_file_info` - Get file metadata
- And more!

### 2. FileSystemPlugin
Semantic Kernel plugin that exposes MCP operations as kernel functions:
```csharp
[KernelFunction]
[Description("Reads the content of a file")]
public string ReadFile(string filePath) { ... }

[KernelFunction]
[Description("Lists all files in a directory")]
public string ListDirectory(string directoryPath = "") { ... }

[KernelFunction]
[Description("Searches for files by pattern")]
public string SearchFiles(string pattern) { ... }

[KernelFunction]
[Description("Gets metadata about a file")]
public string GetFileInfo(string filePath) { ... }
```

### 3. Integration with Semantic Kernel

Plugins are registered with the kernel:
```csharp
var mcpClient = new MCPFilesystemClientWrapper(allowedDir, logger);
kernelBuilder.Plugins.AddFromObject(
    new FileSystemPlugin(mcpClient), 
    nameof(FileSystemPlugin)
);
var kernel = kernelBuilder.Build();
```

Tools are then invoked through the kernel:
```csharp
var readFunc = kernel.Plugins[nameof(FileSystemPlugin)]["ReadFile"];
var result = await kernel.InvokeAsync(readFunc, 
    new() { { "filePath", "config.json" } });
```

## Running the Samples

### Build
```bash
dotnet build
```

### Prerequisites
1. **Node.js** - Must be installed (https://nodejs.org/)
2. **MCP Filesystem Server** - Install with:
```bash
npm install -g @modelcontextprotocol/server-filesystem
```

### Run the Sample
```bash
dotnet run
```

This demonstrates:
- ✓ Starting the official MCP filesystem server
- ✓ JSON-RPC 2.0 communication with the server
- ✓ Listing available MCP tools
- ✓ Listing files through MCP plugin
- ✓ Reading file contents through MCP plugin  
- ✓ Getting file metadata with MCP
- ✓ Using MCP tools through Semantic Kernel

## Alternate Flow: LLM Auto-Invokes Plugin Tools

After the regular explicit flow, the sample includes an alternate mode where you do not call `ListDirectory` directly.
Instead, the user gives a natural-language request and the LLM selects and invokes the plugin function automatically.

Environment variables used:
- `OPENAI_API_KEY` (required)
- `OPENAI_MODEL` (optional, default: `gpt-4o-mini`)

Code pattern:

```csharp
var llmKernelBuilder = Kernel.CreateBuilder();
llmKernelBuilder.AddOpenAIChatCompletion(openAiModel, openAiApiKey);
llmKernelBuilder.Plugins.AddFromObject(new FileSystemPlugin(mcpClient, logger), nameof(FileSystemPlugin));
var llmKernel = llmKernelBuilder.Build();

var settings = new OpenAIPromptExecutionSettings
{
    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
};

var userRequest = "List all directory contents under C:/some/path";
var result = await llmKernel.InvokePromptAsync(
    $"The user asked: {userRequest}. Use plugin tools when needed.",
    new KernelArguments(settings));
```

This is exactly the "user says list all directories, LLM picks the right plugin function" behavior.

### Sample Output
```
✓ MCP Server connected via JSON-RPC 2.0

--- Available MCP Tools ---
• read_text_file: Read the complete contents of a file...
• list_directory: Get a detailed listing of files...
• search_files: Recursively search for files...
(and 12 more tools available)

✓ Semantic Kernel initialized with MCP Filesystem Plugin

--- Example 1: List Files ---
✓ MCP list_directory: C:\Users\arpn\AppData\Local\Temp\MCPServerDemo
Result:
[FILE] config.json
[FILE] notes.txt
[FILE] readme.txt

--- Example 2: Read File ---
✓ MCP ReadFile: readme.txt (52 bytes)
Content:
This is a README file
Created: 3/30/2026 10:42:33 AM
```

## Usage Patterns

### Pattern 1: Direct Tool Invocation Through Kernel
Invoke MCP tools directly through Semantic Kernel:

```csharp
// Initialize with MCP server
using var mcpClient = new MCPFilesystemClient(workingDirectory);
await mcpClient.InitializeAsync();

// Create kernel with MCP plugin
var kernel = Kernel.CreateBuilder()
    .Plugins.AddFromObject(new FileSystemPlugin(mcpClient, logger))
    .Build();

// List files
var listFunc = kernel.Plugins[nameof(FileSystemPlugin)]["ListDirectory"];
var files = await kernel.InvokeAsync(listFunc, new() { { "path", "/path" } });

// Read a file
var readFunc = kernel.Plugins[nameof(FileSystemPlugin)]["ReadFile"];
var content = await kernel.InvokeAsync(readFunc, 
    new() { { "path", "config.json" } });
```

### Pattern 2: LLM-Driven Tool Use with Function Calling
Let an LLM decide which MCP tools to use:

```csharp
// Add OpenAI with MCP plugin
builder.AddOpenAIChatCompletion("gpt-4", apiKey);
var kernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion("gpt-4", apiKey)
    .Plugins.AddFromObject(new FileSystemPlugin(mcpClient, logger))
    .Build();

// Enable automatic MCP tool calling
var settings = new OpenAIPromptExecutionSettings
{ 
    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions 
};

// LLM will autonomously invoke MCP tools
var result = await kernel.InvokePromptAsync(
    "Analyze the project directory and summarize all configuration files",
    new KernelArguments(settings)
);
```


## Security Features

The MCP filesystem plugin provides several security benefits:

- **Directory Sandboxing** - Access is restricted to a configured allowed directory
- **Path Validation** - All file paths are validated to ensure they're within allowed directories
- **Error Handling** - All operations return safe error messages
- **Auditability** - All MCP operations are logged with timestamps
- **Read-Only** - By default, the plugin only provides read operations

Example security check:
```csharp
var fullPath = Path.GetFullPath(requestedPath);
var allowedPath = Path.GetFullPath(_allowedDirectory);

if (!fullPath.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase))
{
    return "ERROR: Access denied - file is outside allowed directory";
}
```

## Real-World Examples

### Example 1: Configuration File Analyzer
An AI agent that finds and analyzes all configuration files:
```csharp
var agent = new MCPFilesystemAgent(kernel, logger);
await agent.RunAutonomousTaskAsync(
    "Find all .json configuration files and extract database settings",
    "/app/config"
);
```

The agent will:
1. Use SearchFiles to find all .json files
2. Use ReadFile to read each config file
3. Use an LLM to extract database settings
4. Return a summary report

### Example 2: Documentation Crawler
Parse and summarize all documentation:
```csharp
var analyzer = new MCPWithLLMExample(kernel, logger, plugin);
var report = await analyzer.AnalyzeDocumentationAsync("/app/docs");
```

### Example 3: Project Health Analysis
Scan a project directory and report:
- File structure overview
- Configuration issues
- Missing required files
- Documentation coverage

## Project Structure

```
MCPServerIntegration/
├── Program.cs                    # Main entry point with examples
├── FileSystemPlugin.cs          # MCP plugin registration
├── MCPClientExample.cs          # Low-level MCP client (JSON-RPC)
├── MCPWithLLMExample.cs        # Advanced LLM integration patterns
├── AdvancedMCPIntegration.cs   # Caching and advanced features
├── MCPServerIntegration.csproj # Project configuration
└── README.md                    # This file
```

## Prerequisites

1. **.NET 8.0+** - [Download](https://dotnet.microsoft.com/download)
2. **Semantic Kernel 1.74.0+** - Included in project file
3. **Optional: LLM Service** - For autonomous tool use:
   - OpenAI API key (for GPT-4, GPT-3.5)
   - Azure OpenAI endpoint and key
   - Or any other Semantic Kernel-compatible LLM

## Getting Started

### 1. Clone/Create Project
```bash
cd MCPServerIntegration
```

### 2. Build
```bash
dotnet build
```

### 3. Run Demo
```bash
dotnet run
```

### 4. Add to Your Project

Copy these files to your project:
- `FileSystemPlugin.cs` - The Semantic Kernel plugin
- `MCPClientExample.cs` - Low-level MCP communication (optional)
- `MCPWithLLMExample.cs` - LLM integration examples (optional)

Then initialize:
```csharp
using MCPServerIntegration;  // The MCP integration namespace

// Create kernel and add plugin
var builder = Kernel.CreateBuilder();
var mcpClient = new MCPFilesystemClient("/allowed/directory");
builder.Plugins.AddFromObject(
    new FileSystemPlugin(mcpClient, logger),
    nameof(FileSystemPlugin)
);
var kernel = builder.Build();

// Use the tools
var readFunc = kernel.Plugins[nameof(FileSystemPlugin)]["ReadFile"];
var result = await kernel.InvokeAsync(readFunc, 
    new() { { "path", "document.txt" } });
```

## Adding an LLM for Autonomous Tool Use

### Option 1: OpenAI
```csharp
var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion(
    modelId: "gpt-4",
    apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")!
);

// Add MCP plugin
var mcpClient = new MCPFilesystemClientWrapper(dir, logger);
builder.Plugins.AddFromObject(new FileSystemPlugin(mcpClient));

var kernel = builder.Build();

// Enable automatic function calling
var settings = new OpenAIPromptExecutionSettings
{
    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
};

// LLM will call MCP tools as needed
var result = await kernel.InvokePromptAsync(
    "Analyze the /data directory and report configuration files",
    new KernelArguments(settings)
);
```

### Option 2: Azure OpenAI
```csharp
var builder = Kernel.CreateBuilder();
builder.AddAzureOpenAIChatCompletion(
    deploymentName: "gpt-4",
    endpoint: new Uri("https://your-resource.openai.azure.com/"),
    apiKey: Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!
);
// ... rest is the same
```

## Common Tasks

### Task 1: Check File Exists and Get Size
```csharp
var infoFunc = kernel.Plugins[nameof(FileSystemPlugin)]["GetFileInfo"];
var info = await kernel.InvokeAsync(infoFunc, 
    new() { { "filePath", "app.json" } });
Console.WriteLine(info);
```

### Task 2: Find All Config Files
```csharp
var searchFunc = kernel.Plugins[nameof(FileSystemPlugin)]["SearchFiles"];
var results = await kernel.InvokeAsync(searchFunc, 
    new() { { "pattern", "config" } });
Console.WriteLine(results);
```

### Task 3: Read Multiple Files and Combine
```csharp
var readFunc = kernel.Plugins[nameof(FileSystemPlugin)]["ReadFile"];
var content1 = await kernel.InvokeAsync(readFunc, 
    new() { { "filePath", "file1.txt" } });
var content2 = await kernel.InvokeAsync(readFunc, 
    new() { { "filePath", "file2.txt" } });
var combined = $"{content1}\n{content2}";
```

## Troubleshooting

### Build Issues
- Ensure .NET 8.0+ is installed: `dotnet --version`
- Clear cache: `dotnet clean && dotnet build`

### Runtime Issues
- Check file paths are absolute or relative to current directory
- Verify the allowed directory exists and is accessible
- Check logs for MCP operation errors

## Architecture Benefits

### Separation of Concerns
- Kernel handles orchestration
- MCP handles filesystem access
- LLM handles reasoning

### Security
- No direct filesystem access for untrusted code
- All paths validated
- Operations logged

### Extensibility
- Easy to add new MCP tools
- Plugin-based architecture
- Works with any LLM service

## Next Steps

1. **Add LLM Integration** - Connect to OpenAI or Azure OpenAI
2. **Extend Tools** - Add write operations, archive handling, etc.
3. **Create Agents** - Build autonomous agents using these tools
4. **Production Ready** - Add error handling, retry logic, caching

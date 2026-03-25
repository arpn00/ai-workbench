using Microsoft.SemanticKernel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace SK.TravelAgent.Api.Services;

/// <summary>
/// Logs every kernel function invocation to the structured logger so
/// tool calls are visible in the API log stream without Console writes.
/// </summary>
public sealed class FunctionCallTraceFilter : IFunctionInvocationFilter
{
    private readonly ILogger<FunctionCallTraceFilter> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FunctionCallTraceFilter(
        ILogger<FunctionCallTraceFilter> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        string pluginName = context.Function.PluginName ?? "UnknownPlugin";
        string functionName = context.Function.Name;

        _logger.LogInformation(
            "[Tool-Start] {Plugin}.{Function}",
            pluginName,
            functionName);

        string argumentPayload = JsonSerializer.Serialize(
            context.Arguments.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString()));
        AgentTraceContext? traceContext = _httpContextAccessor.HttpContext?.RequestServices.GetService<AgentTraceContext>();
        traceContext?.AddToolStart(pluginName, functionName, argumentPayload);

        var stopwatch = Stopwatch.StartNew();

        foreach (var kvp in context.Arguments)
            _logger.LogDebug("  arg.{Key} = {Value}", kvp.Key, kvp.Value);

        await next(context);
        stopwatch.Stop();

        string output = context.Result.GetValue<string>() ?? context.Result.ToString() ?? string.Empty;
        if (output.Length > 220)
            output = string.Concat(output.AsSpan(0, 220), "...");

        traceContext?.AddToolEnd(pluginName, functionName, output, stopwatch.Elapsed.TotalMilliseconds);

        _logger.LogInformation(
            "[Tool-End] {Plugin}.{Function} -> {Output}",
            pluginName,
            functionName,
            output);
    }
}

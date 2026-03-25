using Microsoft.SemanticKernel;

namespace Travel_Agent.Services;

public sealed class FunctionCallTraceFilter : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"[Tool-Start] {context.Function.PluginName}.{context.Function.Name}");
        Console.ResetColor();

        foreach (var kvp in context.Arguments)
        {
            Console.WriteLine($"  arg.{kvp.Key} = {kvp.Value}");
        }

        await next(context);

        string output = context.Result.GetValue<string>() ?? context.Result.ToString();
        string oneLine = output.Replace(Environment.NewLine, " ");
        if (oneLine.Length > 220)
        {
            oneLine = oneLine[..220] + "...";
        }

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"[Tool-End] {context.Function.PluginName}.{context.Function.Name} -> {oneLine}");
        Console.ResetColor();
        Console.WriteLine();
    }
}

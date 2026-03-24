using Microsoft.Extensions.Configuration;

namespace Demo.Common;

public sealed class AzureOpenAISettings
{
	public string? Endpoint { get; init; }
	public string? ApiKey { get; init; }
	public string DeploymentName { get; init; } = "gpt-4o-mini";

	public bool IsConfigured =>
		!string.IsNullOrWhiteSpace(Endpoint) &&
		!string.IsNullOrWhiteSpace(ApiKey);
}

public static class DemoConfiguration
{
	public static AzureOpenAISettings LoadAzureOpenAISettings(string appSettingsFile = "appsettings.json")
	{
		var configuration = new ConfigurationBuilder()
			.AddJsonFile(appSettingsFile, optional: true, reloadOnChange: false)
			.AddEnvironmentVariables()
			.Build();

		string? endpoint = ReadSetting("AZURE_OPENAI_ENDPOINT", "AzureOpenAI:Endpoint", configuration);
		string? apiKey = ReadSetting("AZURE_OPENAI_API_KEY", "AzureOpenAI:ApiKey", configuration);
		string deploymentName = ReadSetting("AZURE_OPENAI_DEPLOYMENT", "AzureOpenAI:DeploymentName", configuration)
			?? "gpt-4o-mini";

		return new AzureOpenAISettings
		{
			Endpoint = endpoint,
			ApiKey = apiKey,
			DeploymentName = deploymentName
		};
	}

	private static string? ReadSetting(string envKey, string appSettingsKey, IConfiguration configuration)
	{
		return Environment.GetEnvironmentVariable(envKey) ?? configuration[appSettingsKey];
	}
}

public static class ConsolePrompt
{
	public static string Required(string label)
	{
		while (true)
		{
			System.Console.Write(label);
			string? value = System.Console.ReadLine();
			if (!string.IsNullOrWhiteSpace(value))
			{
				return value.Trim();
			}

			System.Console.ForegroundColor = ConsoleColor.Yellow;
			System.Console.WriteLine("Input is required. Please try again.");
			System.Console.ResetColor();
		}
	}

	public static int PositiveInt(string label)
	{
		while (true)
		{
			string raw = Required(label);
			if (int.TryParse(raw, out int value) && value > 0)
			{
				return value;
			}

			System.Console.ForegroundColor = ConsoleColor.Yellow;
			System.Console.WriteLine("Please enter a valid positive number.");
			System.Console.ResetColor();
		}
	}
}

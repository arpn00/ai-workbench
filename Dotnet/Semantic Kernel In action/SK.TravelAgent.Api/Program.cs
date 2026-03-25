using Demo.Common;
using SK.TravelAgent.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Azure OpenAI configuration ────────────────────────────────────────────────
// Reads from env vars (AZURE_OPENAI_ENDPOINT / AZURE_OPENAI_API_KEY /
// AZURE_OPENAI_DEPLOYMENT) or AzureOpenAI section in appsettings.json.
AzureOpenAISettings aoaiSettings = DemoConfiguration.LoadAzureOpenAISettings();
if (!aoaiSettings.IsConfigured)
    throw new InvalidOperationException(
        "Azure OpenAI settings are missing. " +
        "Set AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_API_KEY before starting the API.");

builder.Services.AddSingleton(aoaiSettings);
builder.Services.AddSingleton<FunctionCallTraceFilter>();

// ── Semantic Kernel (singleton) ───────────────────────────────────────────────
// Kernel is expensive to build (registers plugins + agent wrappers) so it is
// shared across all requests. ILoggerFactory is provided by ASP.NET Core DI.
builder.Services.AddSingleton(sp =>
{
    return KernelFactory.Create(aoaiSettings, sp);
});

// ── Chat session store (singleton) ───────────────────────────────────────────
// Holds ChatHistory per session ID in memory. Survives for process lifetime.
builder.Services.AddSingleton<ChatSessionStore>();

// ── Chat service (scoped) ────────────────────────────────────────────────────
builder.Services.AddScoped<TravelChatService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AgentTraceContext>();

// ── ASP.NET Core infrastructure ──────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "SK.TravelAgent.Api", Version = "v1",
        Description = "LLM-orchestrated travel planning API built with Semantic Kernel" }));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SK.TravelAgent.Api v1"));
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();

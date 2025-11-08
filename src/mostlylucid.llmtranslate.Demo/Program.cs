using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using mostlylucid.llmtranslate.Extensions;
using mostlylucid.llmtranslate.Hubs;
using mostlylucid.llmtranslate.Services;
using mostlylucid.llmtranslate.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Basic logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Register IAiTranslationProvider via configuration in AddAutoTranslateFromConfiguration below
// (Remove demo echo provider to use real backends configured in appsettings.json)

// Add MVC + Razor
builder.Services.AddControllersWithViews();

// Add AutoTranslate from configuration (section "LlmTranslate")
builder.Services.AddAutoTranslateFromConfiguration(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

// Map llmtranslate endpoints (controllers + SignalR hub)
app.MapLlmTranslateEndpoints();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Demo AI provider implementation
public class DemoEchoAiProvider : IAiTranslationProvider
{
    public Task<string> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = "en", CancellationToken ct = default)
    {
        // For demo purposes, just append [lang]
        return Task.FromResult($"{text} [{targetLanguage}]");
    }

    public Task<Dictionary<string, string>> TranslateBatchAsync(Dictionary<string, string> items, string targetLanguage, string? sourceLanguage = "en", CancellationToken ct = default)
    {
        var result = items.ToDictionary(kv => kv.Key, kv => $"{kv.Value} [{targetLanguage}]\u200B");
        return Task.FromResult(result);
    }
}
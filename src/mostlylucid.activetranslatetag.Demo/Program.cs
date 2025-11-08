using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using mostlylucid.activetranslatetag.Extensions;
using mostlylucid.activetranslatetag.Hubs;
using mostlylucid.activetranslatetag.Services;
using mostlylucid.activetranslatetag.Controllers;

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

// Demo AI provider implementation (only used if no provider configured in appsettings.json)
public class DemoEchoAiProvider : IAiTranslationProvider
{
    public Task<string> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = "en", string? description = null, CancellationToken ct = default)
    {
        // For demo purposes, just append [lang]
        var result = description != null ? $"{text} [{targetLanguage}] ({description})" : $"{text} [{targetLanguage}]";
        return Task.FromResult(result);
    }

    public Task<Dictionary<string, string>> TranslateBatchAsync(Dictionary<string, string> items, string targetLanguage, string? sourceLanguage = "en", string? description = null, CancellationToken ct = default)
    {
        var suffix = description != null ? $" [{targetLanguage}] ({description})" : $" [{targetLanguage}]";
        var result = items.ToDictionary(kv => kv.Key, kv => $"{kv.Value}{suffix}");
        return Task.FromResult(result);
    }
}
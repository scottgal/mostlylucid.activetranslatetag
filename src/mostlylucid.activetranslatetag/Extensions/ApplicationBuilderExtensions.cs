using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using mostlylucid.activetranslatetag.Hubs;

namespace mostlylucid.activetranslatetag.Extensions;

/// <summary>
/// Endpoint mapping helpers for llmtranslate.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Maps the LanguageController routes and the SignalR TranslationHub with a single call.
    /// Call this after building the app, e.g.:
    /// var app = builder.Build();
    /// app.MapLlmTranslateEndpoints();
    /// </summary>
    /// <param name="endpoints">Endpoint route builder (typically WebApplication)</param>
    /// <param name="hubPath">Optional hub path override. Default is TranslationHub.HubPath ("/hubs/translation").</param>
    /// <returns>The same endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapLlmTranslateEndpoints(this IEndpointRouteBuilder endpoints, string? hubPath = null)
    {
        // Map MVC controller routes for LanguageController (attribute routing)
        endpoints.MapControllers();

        // Map SignalR hub for translation progress and updates
        endpoints.MapHub<TranslationHub>(hubPath ?? TranslationHub.HubPath);

        return endpoints;
    }
}

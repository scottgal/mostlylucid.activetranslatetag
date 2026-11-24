using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using mostlylucid.activetranslatetag.Active;
using mostlylucid.activetranslatetag.ViewComponents;

namespace mostlylucid.activetranslatetag.Extensions;

public static class ActiveTranslationExtensions
{
    public static IServiceCollection AddActiveTranslation(this IServiceCollection services, Action<ActiveTranslationOptions>? configure = null)
    {
        services.AddOptions<ActiveTranslationOptions>()
            .Configure(o => configure?.Invoke(o))
            .Validate(o => o.MemoryCacheEntries >= 0 && o.QueueCapacity > 0 && o.TranslationTimeout > TimeSpan.Zero,
                "Cache size must be nonnegative; queue capacity and timeout must be positive.").ValidateOnStart();
        services.AddDataProtection();
        services.AddHttpContextAccessor();
        services.AddSignalR();
        services.AddControllersWithViews().AddApplicationPart(typeof(ActiveTranslateViewComponent).Assembly);
        services.AddSingleton<SqliteTranslationStore>();
        services.AddSingleton<TranslationTokens>();
        services.AddSingleton<ActiveTranslationQueue>();
        services.AddHostedService(sp => sp.GetRequiredService<ActiveTranslationQueue>());
        return services;
    }

    public static IEndpointRouteBuilder MapActiveTranslation(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(ActiveTranslationFragment.Path, ActiveTranslationFragment.GetAsync);
        endpoints.MapHub<ActiveTranslationHub>(ActiveTranslationHub.Path);
        return endpoints;
    }
}

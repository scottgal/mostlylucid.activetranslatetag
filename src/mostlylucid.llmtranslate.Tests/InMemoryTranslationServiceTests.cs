using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using mostlylucid.llmtranslate.Models;
using mostlylucid.llmtranslate.Services;
using mostlylucid.llmtranslate.Services.InMemory;
using Xunit;

namespace mostlylucid.llmtranslate.Tests;

public class InMemoryTranslationServiceTests
{
    private static InMemoryTranslationService CreateService(out InMemoryStore store)
    {
        store = new InMemoryStore();
        var ai = new FakeAiProvider();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var req = new RequestTranslationCache();
        var logger = new NullLogger<InMemoryTranslationService>();
        return new InMemoryTranslationService(store, ai, cache, req, logger, enableMemoryCache: true, cacheDurationMinutes: 5);
    }

    [Fact]
    public async Task EnsureString_Then_Get_English_ReturnsDefault()
    {
        var svc = CreateService(out var store);
        await svc.EnsureStringAsync("home.title", "Welcome");
        var text = await svc.GetAsync("home.title", "en");
        Assert.Equal("Welcome", text);
    }

    [Fact]
    public async Task TranslateAll_Produces_TargetLanguage_Strings()
    {
        var svc = CreateService(out var store);
        await svc.EnsureStringAsync("home.lead", "Hello world");
        await svc.EnsureStringAsync("home.item1", "Item one");

        var progressEvents = new List<TranslationProgress>();
        var progress = new Progress<TranslationProgress>(p => progressEvents.Add(p));

        var count = await svc.TranslateAllStringsAsync("fr", overwriteExisting: true, progress: progress);

        Assert.Equal(2, count);
        var t1 = await svc.GetAsync("home.lead", "fr");
        var t2 = await svc.GetAsync("home.item1", "fr");
        Assert.Contains("<fr>", t1);
        Assert.Contains("<fr>", t2);
        Assert.NotEmpty(progressEvents);
    }
}

using System.Net;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using mostlylucid.activetranslatetag.Active;
using mostlylucid.activetranslatetag.Extensions;
using mostlylucid.activetranslatetag.Services;
using Xunit;

namespace mostlylucid.activetranslatetag.Tests;

public class ActiveTranslationTests
{
    [Fact]
    public void Markup_PreservesGreaterThanInsideQuotedAttribute()
    {
        const string source = """<span title="3 > 2">Hello</span>""";
        var markup = TranslationMarkup.Parse(source);

        Assert.Equal("""<span title="3 > 2">Bonjour</span>""",
            markup.Render(markup.Text.Replace("Hello", "Bonjour", StringComparison.Ordinal)));
    }

    [Fact]
    public void Markup_EncodesModelHtmlAndPreservesOriginalLinks()
    {
        var markup = TranslationMarkup.Parse("<p>Hello <a href=\"/guide\">{name}</a> &amp; friends</p>");
        var html = markup.Render(markup.Text.Replace("Hello", "<script>alert(1)</script>Bonjour"));
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("<a href=\"/guide\">{name}</a>", html);
        Assert.Contains("&amp; friends", html);
    }

    [Fact]
    public void Markup_RejectsMissingOrReorderedPlaceholders()
    {
        var markup = TranslationMarkup.Parse("<p>Hello <strong>{name}</strong></p>");
        Assert.Throws<InvalidOperationException>(() => markup.Render(markup.Text.Replace("{name}", "Bob")));
        Assert.Throws<InvalidOperationException>(() => markup.Render(markup.Text.Replace("[[HTML_0]]", "[[HTML_X]]")));
        Assert.Throws<InvalidOperationException>(() => markup.Render(markup.Text.Replace("[[HTML_0]]", "[[HTML_1]]").Replace("[[HTML_2]]", "[[HTML_0]]")));
        Assert.Throws<ArgumentException>(() => TranslationMarkup.Parse("<script>alert(1)</script>"));
    }

    [Fact]
    public void Markup_LiteralMarkerTextCannotCollideWithServerBoundaries()
    {
        var markup = TranslationMarkup.Parse("<p>[[HTML_0]] Hello</p>");
        Assert.DoesNotContain("[[HTML_0]]", markup.Tags.Keys);
        Assert.Equal("<p>[[HTML_0]] Hello</p>", markup.Render(markup.Text));
    }

    [Fact]
    public void Keys_ChangeWithContentLanguageContextAndModel()
    {
        var request = new TranslationRequest(TranslationMarkup.Parse("Hello"), "en", "fr", "forum", "model-v1");
        Assert.Equal(request.Id, (request with { }).Id);
        Assert.NotEqual(request.Id, (request with { TargetLanguage = "de" }).Id);
        Assert.NotEqual(request.Id, (request with { Description = "formal" }).Id);
        Assert.NotEqual(request.Id, (request with { CacheNamespace = "model-v2" }).Id);
        Assert.NotEqual(request.Id, (request with { Markup = TranslationMarkup.Parse("Hello again") }).Id);
    }

    [Fact]
    public async Task Sqlite_PersistsAcrossInstancesAndDoesNotLoseCompletedValues()
    {
        var path = TemporaryDatabase();
        var options = Options.Create(new ActiveTranslationOptions { DatabasePath = path, MemoryCacheEntries = 1 });
        var request = new TranslationRequest(TranslationMarkup.Parse("Hello"), "en", "fr", null, "v1");
        try
        {
            using (var store = new SqliteTranslationStore(options))
            {
                await store.SaveRequestAsync(request);
                await store.SetHtmlAsync(request.Id, "Bonjour");
                await store.SaveRequestAsync(request); // INSERT OR IGNORE must preserve the completed value.
            }
            using var reopened = new SqliteTranslationStore(options);
            Assert.Equal("Bonjour", await reopened.GetHtmlAsync(request.Id));
            Assert.Equal(request.Id, (await reopened.GetRequestAsync(request.Id))!.Id);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task ActiveFlow_RenderQueueSignalRFragmentAndCacheHit()
    {
        var path = TemporaryDatabase();
        var provider = new ControlledProvider();
        using var host = await StartHost(path, provider);
        try
        {
            var client = host.GetTestClient();
            // Razor must render the English fallback even while inference is blocked.
            var page = await client.GetStringAsync("/");
            var doc = new HtmlDocument(); doc.LoadHtml(page);
            var elements = doc.DocumentNode.SelectNodes("//*[@data-translation-id]");
            Assert.NotNull(elements);
            Assert.Equal(3, elements.Count);
            Assert.Equal(3, elements.Select(e => e.Id).Distinct().Count());
            Assert.Equal(elements[0].GetAttributeValue("data-translation-id", ""), elements[2].GetAttributeValue("data-translation-id", ""));
            var token = elements[0].GetAttributeValue("data-translation-token", "");
            Assert.NotEmpty(token);
            Assert.Contains("hx-trigger=\"translation-ready\"", page);
            Assert.Contains("Hello, forum!", page);
            Assert.DoesNotContain("Bonjour, forum!", page);
            // Inference begins from the page render while the browser is still disconnected.
            await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var fragmentUrl = WebUtility.HtmlDecode(elements[0].GetAttributeValue("hx-get", ""));
            Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync(fragmentUrl)).StatusCode);
            var id = elements[0].GetAttributeValue("data-translation-id", "");
            var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var failed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var connection = new HubConnectionBuilder().WithUrl("http://localhost" + ActiveTranslationHub.Path, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => host.GetTestServer().CreateHandler();
            }).Build();
            connection.On<ReadyMessage>("TranslationReady", message =>
            {
                if (message.Id == id) ready.TrySetResult(message.Id);
            });
            connection.On<ReadyMessage>("TranslationFailed", message => failed.TrySetResult(message.Id));
            await connection.StartAsync();

            // A stored but unqueued source models an interrupted render or an old token after restart.
            // Neither GET nor Watch may turn it into a new job.
            var orphan = new TranslationRequest(TranslationMarkup.Parse("Unqueued"), "en", "fr", null, "orphan");
            var store = host.Services.GetRequiredService<SqliteTranslationStore>();
            await store.SaveRequestAsync(orphan);
            var orphanToken = host.Services.GetRequiredService<TranslationTokens>().Create(orphan.Id);
            Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/_active-translate/fragment?token=" + orphanToken)).StatusCode);
            await connection.InvokeAsync("Watch", orphanToken);
            Assert.Equal(orphan.Id, await failed.Task.WaitAsync(TimeSpan.FromSeconds(10)));

            var secondToken = elements[1].GetAttributeValue("data-translation-token", "");
            var secondId = elements[1].GetAttributeValue("data-translation-id", "");
            var secondReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var secondSubscription = connection.On<ReadyMessage>("TranslationReady", message =>
            {
                if (message.Id == secondId) secondReady.TrySetResult();
            });
            await connection.InvokeAsync("Watch", secondToken);
            await connection.InvokeAsync("Watch", token);
            provider.Release.TrySetResult();
            Assert.Equal(id, await ready.Task.WaitAsync(TimeSpan.FromSeconds(10)));

            // A later subscription replays readiness from storage.
            await connection.InvokeAsync("Unwatch", token);
            ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            await connection.InvokeAsync("Watch", token);
            Assert.Equal(id, await ready.Task.WaitAsync(TimeSpan.FromSeconds(10)));
            var response = await client.GetAsync(fragmentUrl);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Bonjour, forum!", html);
            Assert.Equal("translation-swapped", response.Headers.GetValues("HX-Trigger").Single());
            Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/_active-translate/fragment?token=tampered")).StatusCode);
            var cachedPage = await client.GetStringAsync("/");
            Assert.Contains("Bonjour, forum!", cachedPage);
            await secondReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
            // Five text fragments across two unique sources; the repeated post adds no inference.
            Assert.Equal(5, provider.Calls);
        }
        finally
        {
            provider.Release.TrySetResult();
            await host.StopAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public async Task Queue_ModelFailureDoesNotCacheFallbackAsTranslation()
    {
        var path = TemporaryDatabase();
        var provider = new ControlledProvider { Fail = true }; provider.Release.TrySetResult();
        using var host = await StartHost(path, provider);
        try
        {
            var store = host.Services.GetRequiredService<SqliteTranslationStore>();
            var request = new TranslationRequest(TranslationMarkup.Parse("Hello"), "en", "fr", null, "failed");
            await store.SaveRequestAsync(request);
            var result = await (await host.Services.GetRequiredService<ActiveTranslationQueue>().EnqueueAsync(request.Id)).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsType<TranslationFailed>(result.Value);
            Assert.Null(await store.GetHtmlAsync(request.Id));
        }
        finally
        {
            await host.StopAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    private static string TemporaryDatabase() => Path.Combine(Path.GetTempPath(), "active-translation-tests", Guid.NewGuid().ToString("N"), "cache.db");

    private static async Task<IHost> StartHost(string path, ControlledProvider provider) => await new HostBuilder()
        .ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
        {
            services.AddActiveTranslation(options => { options.DatabasePath = path; options.MemoryCacheEntries = 2; });
            services.AddControllersWithViews().AddApplicationPart(typeof(mostlylucid.activetranslatetag.Demo.Controllers.HomeController).Assembly);
            services.AddSingleton<IAiTranslationProvider>(provider);
        }).Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => { endpoints.MapActiveTranslation(); endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); });
        })).StartAsync();

    private sealed record ReadyMessage(string Id);
    private sealed class ControlledProvider : IAiTranslationProvider
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;
        public bool Fail;
        public async Task<string> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = "en", string? description = null, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Calls);
            Started.TrySetResult();
            await Release.Task.WaitAsync(ct);
            if (Fail) throw new InvalidOperationException("Model unavailable.");
            return text.Replace("Hello", "Bonjour");
        }
        public Task<Dictionary<string, string>> TranslateBatchAsync(Dictionary<string, string> items, string targetLanguage, string? sourceLanguage = "en", string? description = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}

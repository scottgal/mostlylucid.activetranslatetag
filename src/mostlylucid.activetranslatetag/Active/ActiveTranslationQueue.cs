using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using mostlylucid.activetranslatetag.Services;

namespace mostlylucid.activetranslatetag.Active;

/// <summary>One bounded, deduplicated queue per process. Active rendering never waits for inference.</summary>
public sealed class ActiveTranslationQueue : BackgroundService
{
    private readonly Channel<string> _queue;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<TranslationResult>> _pending = new();
    private readonly SqliteTranslationStore _store;
    private readonly IServiceScopeFactory _scopes;
    private readonly IHubContext<ActiveTranslationHub, IActiveTranslationClient> _hub;
    private readonly ILogger<ActiveTranslationQueue> _logger;
    private readonly ActiveTranslationOptions _options;

    public ActiveTranslationQueue(SqliteTranslationStore store, IServiceScopeFactory scopes,
        IHubContext<ActiveTranslationHub, IActiveTranslationClient> hub, IOptions<ActiveTranslationOptions> options, ILogger<ActiveTranslationQueue> logger)
    {
        _store = store; _scopes = scopes; _hub = hub; _logger = logger; _options = options.Value;
        _queue = Channel.CreateBounded<string>(new BoundedChannelOptions(_options.QueueCapacity)
        { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    }

    internal bool IsPending(string id) => _pending.ContainsKey(id);

    public async Task<Task<TranslationResult>> EnqueueAsync(string id, CancellationToken ct = default)
    {
        var html = await _store.GetHtmlAsync(id, ct);
        if (html != null) return Task.FromResult<TranslationResult>(new TranslationReady(html));
        var completion = new TaskCompletionSource<TranslationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var existing = _pending.GetOrAdd(id, completion);
        if (existing != completion) return existing.Task;
        if (!_queue.Writer.TryWrite(id))
        {
            _pending.TryRemove(id, out _);
            completion.TrySetResult(new TranslationFailed("The translation queue is full. Try again shortly."));
        }
        return completion.Task;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var id in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                TranslationResult result;
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    timeout.CancelAfter(_options.TranslationTimeout);
                    var ct = timeout.Token;
                    var html = await _store.GetHtmlAsync(id, ct);
                    if (html == null)
                    {
                        var request = await _store.GetRequestAsync(id, ct) ?? throw new InvalidOperationException("Missing translation source.");
                        using var scope = _scopes.CreateScope();
                        var provider = scope.ServiceProvider.GetRequiredService<IAiTranslationProvider>();
                        html = await request.Markup.TranslateAsync(provider, request.TargetLanguage, request.SourceLanguage, request.Description, ct);
                        await _store.SetHtmlAsync(id, html, ct);
                    }
                    result = new TranslationReady(html);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Translation failed for {Id}", id);
                    result = new TranslationFailed("Translation unavailable. Original content retained.");
                }
                // Complete waiters even if the notification transport is unavailable.
                if (_pending.TryRemove(id, out var completion)) completion.TrySetResult(result);
                try
                {
                    var client = _hub.Clients.Group(id);
                    var notice = new TranslationNotice(id);
                    await (result switch
                    {
                        TranslationReady => client.TranslationReady(notice),
                        TranslationFailed => client.TranslationFailed(notice)
                    }).WaitAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                { _logger.LogWarning(ex, "Notification failed for {Id}; reconnect checks the cache", id); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            _queue.Writer.TryComplete();
            foreach (var (id, completion) in _pending)
                completion.TrySetResult(new TranslationFailed("Application is stopping."));
            _pending.Clear();
        }
    }
}

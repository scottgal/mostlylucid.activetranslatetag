using Microsoft.AspNetCore.SignalR;

namespace mostlylucid.activetranslatetag.Active;

public sealed record TranslationNotice(string Id);

public interface IActiveTranslationClient
{
    Task TranslationReady(TranslationNotice notice);
    Task TranslationFailed(TranslationNotice notice);
}

public sealed class ActiveTranslationHub(TranslationTokens tokens, SqliteTranslationStore store, ActiveTranslationQueue queue) : Hub<IActiveTranslationClient>
{
    public const string Path = "/_active-translate/hub";

    public async Task Watch(string token)
    {
        var id = tokens.Read(token);
        await Groups.AddToGroupAsync(Context.ConnectionId, id, Context.ConnectionAborted);
        // Join first to catch future notifications. Check pending state before reading the
        // cache because the worker saves the fragment before removing the pending entry.
        var pending = queue.IsPending(id);
        var html = await store.GetHtmlAsync(id, Context.ConnectionAborted);
        if (html != null)
            await Clients.Caller.TranslationReady(new(id)).WaitAsync(Context.ConnectionAborted);
        else if (!pending)
            await Clients.Caller.TranslationFailed(new(id)).WaitAsync(Context.ConnectionAborted);
    }

    public Task Unwatch(string token) => Groups.RemoveFromGroupAsync(Context.ConnectionId, tokens.Read(token));
}

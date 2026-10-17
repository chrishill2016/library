using Bookshelf.Models;
using Microsoft.JSInterop;

namespace Bookshelf.Services.Storage;

public sealed class IndexedDbLocalStore(IJSRuntime js) : ILocalStore, IAsyncDisposable
{
    private const string Books = "books";
    private const string Outbox = "outbox";
    private const string Settings = "settings";

    private readonly Lazy<Task<IJSObjectReference>> _module =
        new(() => js.InvokeAsync<IJSObjectReference>("import", "./js/local-store.js").AsTask());

    public async Task<IReadOnlyList<Book>> GetBooksAsync() =>
        await (await _module.Value).InvokeAsync<Book[]>("getAll", Books);

    public async Task<Book?> GetBookAsync(string id) =>
        await (await _module.Value).InvokeAsync<Book?>("get", Books, id);

    public async Task PutBookAsync(Book book, OutboxEntry? outboxEntry = null) =>
        await (await _module.Value).InvokeVoidAsync("putWithOutbox", Books, book, outboxEntry);

    public async Task<IReadOnlyList<OutboxEntry>> GetOutboxAsync() =>
        await (await _module.Value).InvokeAsync<OutboxEntry[]>("getAll", Outbox);

    public async Task<int> GetOutboxCountAsync() =>
        await (await _module.Value).InvokeAsync<int>("count", Outbox);

    public async Task RemoveFromOutboxAsync(string entryId) =>
        await (await _module.Value).InvokeVoidAsync("remove", Outbox, entryId);

    public async Task<string?> GetSettingAsync(string key) =>
        (await (await _module.Value).InvokeAsync<SettingRecord?>("get", Settings, key))?.Value;

    public async Task SetSettingAsync(string key, string value) =>
        await (await _module.Value).InvokeVoidAsync("put", Settings, new SettingRecord(key, value));

    public async Task RemoveSettingAsync(string key) =>
        await (await _module.Value).InvokeVoidAsync("remove", Settings, key);

    public async Task ClearLibraryDataAsync() =>
        await (await _module.Value).InvokeVoidAsync("clear", Books, Outbox);

    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated)
        {
            try
            {
                await (await _module.Value).DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    private sealed record SettingRecord(string Key, string Value);
}

using Bookshelf.Models;
using Bookshelf.Services.Storage;

namespace Bookshelf.Tests.Fakes;

public sealed class InMemoryLocalStore : ILocalStore
{
    public Dictionary<string, Book> Books { get; } = [];
    public Dictionary<string, Reading> Readings { get; } = [];
    public Dictionary<string, OutboxEntry> Outbox { get; } = [];
    public Dictionary<string, string> Settings { get; } = [];

    /// <summary>Runs after an outbox read, to simulate edits made mid-sync.</summary>
    public Action? AfterGetOutbox { get; set; }

    public Task<IReadOnlyList<Book>> GetBooksAsync() => Task.FromResult<IReadOnlyList<Book>>([.. Books.Values]);

    public Task<Book?> GetBookAsync(string id) => Task.FromResult(Books.GetValueOrDefault(id));

    public Task PutBookAsync(Book book, OutboxEntry? outboxEntry = null)
    {
        Books[book.Id] = book;
        if (outboxEntry is not null)
        {
            Outbox[outboxEntry.Id] = outboxEntry;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Reading>> GetReadingsAsync() => Task.FromResult<IReadOnlyList<Reading>>([.. Readings.Values]);

    public Task<Reading?> GetReadingAsync(string id) => Task.FromResult(Readings.GetValueOrDefault(id));

    public Task PutReadingAsync(Reading reading, OutboxEntry? outboxEntry = null)
    {
        Readings[reading.Id] = reading;
        if (outboxEntry is not null)
        {
            Outbox[outboxEntry.Id] = outboxEntry;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OutboxEntry>> GetOutboxAsync()
    {
        IReadOnlyList<OutboxEntry> snapshot = [.. Outbox.Values];
        var hook = AfterGetOutbox;
        AfterGetOutbox = null;
        hook?.Invoke();
        return Task.FromResult(snapshot);
    }

    public Task<int> GetOutboxCountAsync() => Task.FromResult(Outbox.Count);

    public Task RemoveFromOutboxAsync(string entryId)
    {
        Outbox.Remove(entryId);
        return Task.CompletedTask;
    }

    public Task<string?> GetSettingAsync(string key) => Task.FromResult(Settings.GetValueOrDefault(key));

    public Task SetSettingAsync(string key, string value)
    {
        Settings[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveSettingAsync(string key)
    {
        Settings.Remove(key);
        return Task.CompletedTask;
    }

    public Task ClearLibraryDataAsync()
    {
        Books.Clear();
        Readings.Clear();
        Outbox.Clear();
        return Task.CompletedTask;
    }
}

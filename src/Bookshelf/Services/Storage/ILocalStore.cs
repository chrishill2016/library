using Bookshelf.Models;

namespace Bookshelf.Services.Storage;

/// <summary>On-device storage. All reads and writes hit this first.</summary>
public interface ILocalStore
{
    /// <summary>All books, including soft-deleted ones.</summary>
    Task<IReadOnlyList<Book>> GetBooksAsync();

    Task<Book?> GetBookAsync(string id);

    /// <summary>Saves a book and, when given, queues it for sync in the same transaction.</summary>
    Task PutBookAsync(Book book, OutboxEntry? outboxEntry = null);

    /// <summary>Everyone's readings (ratings, reviews, read dates) for all books.</summary>
    Task<IReadOnlyList<Reading>> GetReadingsAsync();

    Task<Reading?> GetReadingAsync(string id);

    /// <summary>Saves a reading and, when given, queues it for sync in the same transaction.</summary>
    Task PutReadingAsync(Reading reading, OutboxEntry? outboxEntry = null);

    Task<IReadOnlyList<OutboxEntry>> GetOutboxAsync();

    Task<int> GetOutboxCountAsync();

    Task RemoveFromOutboxAsync(string entryId);

    Task<string?> GetSettingAsync(string key);

    Task SetSettingAsync(string key, string value);

    Task RemoveSettingAsync(string key);

    /// <summary>Deletes all books, readings and queued changes (used when a different account signs in).</summary>
    Task ClearLibraryDataAsync();
}

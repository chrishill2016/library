namespace Bookshelf.Models;

/// <summary>
/// A local change waiting to be pushed to the remote database.
/// There is at most one entry per entity; re-queuing replaces it.
/// </summary>
public sealed record OutboxEntry(string Id, string EntityType, string EntityId, DateTimeOffset QueuedAt)
{
    public const string BookType = "book";
    public const string ReadingType = "reading";

    public static OutboxEntry ForBook(string bookId, DateTimeOffset queuedAt) =>
        new($"{BookType}:{bookId}", BookType, bookId, queuedAt);

    public static OutboxEntry ForReading(string readingId, DateTimeOffset queuedAt) =>
        new($"{ReadingType}:{readingId}", ReadingType, readingId, queuedAt);
}

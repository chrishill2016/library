using Bookshelf.Models;
using Bookshelf.Services.Sync;

namespace Bookshelf.Tests.Fakes;

/// <summary>Remote store whose cursor is a simple change sequence number.</summary>
public sealed class FakeRemoteStore : IRemoteStore
{
    private readonly List<(long Seq, Book Book)> _log = [];
    private long _seq;

    public bool IsConfigured { get; set; } = true;
    public Exception? FailWith { get; set; }
    public List<IReadOnlyList<Book>> Pushes { get; } = [];
    public List<string?> CursorsRequested { get; } = [];

    public Dictionary<string, Book> Books { get; } = [];

    /// <summary>Simulates another device writing a book.</summary>
    public void Seed(Book book)
    {
        Books[book.Id] = book;
        _log.Add((++_seq, book));
    }

    public Task<RemoteChangeSet> GetChangesAsync(string? cursor, CancellationToken cancellationToken = default)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        CursorsRequested.Add(cursor);
        var since = cursor is null ? 0 : long.Parse(cursor);
        var changes = _log.Where(e => e.Seq > since).ToList();
        IReadOnlyList<Book> books = [.. changes.Select(e => e.Book)];
        return Task.FromResult(new RemoteChangeSet(books, changes.Count == 0 ? null : changes[^1].Seq.ToString()));
    }

    public Task PutBooksAsync(IReadOnlyList<Book> books, CancellationToken cancellationToken = default)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        Pushes.Add(books);
        foreach (var book in books)
        {
            Seed(book);
        }

        return Task.CompletedTask;
    }
}

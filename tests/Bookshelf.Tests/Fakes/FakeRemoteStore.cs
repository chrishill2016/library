using Bookshelf.Models;
using Bookshelf.Services.Sync;

namespace Bookshelf.Tests.Fakes;

/// <summary>Remote store whose cursor is a simple change sequence number (shared by books and readings).</summary>
public sealed class FakeRemoteStore : IRemoteStore
{
    private readonly List<(long Seq, Book? Book, Reading? Reading)> _log = [];
    private long _seq;

    private bool _isConfigured = true;

    public bool IsConfigured
    {
        get => _isConfigured;
        set
        {
            _isConfigured = value;
            ConfigurationChanged?.Invoke();
        }
    }

    public event Action? ConfigurationChanged;
    public Exception? FailWith { get; set; }
    public List<IReadOnlyList<Book>> Pushes { get; } = [];
    public List<IReadOnlyList<Reading>> ReadingPushes { get; } = [];
    public List<string?> CursorsRequested { get; } = [];

    public Dictionary<string, Book> Books { get; } = [];
    public Dictionary<string, Reading> Readings { get; } = [];

    /// <summary>Simulates another device writing a book.</summary>
    public void Seed(Book book)
    {
        Books[book.Id] = book;
        _log.Add((++_seq, book, null));
    }

    /// <summary>Simulates another device writing a reading.</summary>
    public void Seed(Reading reading)
    {
        Readings[reading.Id] = reading;
        _log.Add((++_seq, null, reading));
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
        IReadOnlyList<Book> books = [.. changes.Where(e => e.Book is not null).Select(e => e.Book!)];
        IReadOnlyList<Reading> readings = [.. changes.Where(e => e.Reading is not null).Select(e => e.Reading!)];
        return Task.FromResult(new RemoteChangeSet(books, readings, changes.Count == 0 ? null : changes[^1].Seq.ToString()));
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

    public Task PutReadingsAsync(IReadOnlyList<Reading> readings, CancellationToken cancellationToken = default)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        ReadingPushes.Add(readings);
        foreach (var reading in readings)
        {
            Seed(reading);
        }

        return Task.CompletedTask;
    }
}

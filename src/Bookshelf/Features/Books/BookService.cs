using Bookshelf.Models;
using Bookshelf.Services.Storage;
using Bookshelf.Services.Sync;

namespace Bookshelf.Features.Books;

/// <summary>Reads and writes books via the local store and queues changes for sync.</summary>
public sealed class BookService : IDisposable
{
    private readonly ILocalStore _store;
    private readonly ISyncService _sync;
    private readonly TimeProvider _time;
    private List<Book>? _cache;

    public BookService(ILocalStore store, ISyncService sync, TimeProvider time)
    {
        _store = store;
        _sync = sync;
        _time = time;
        _sync.RemoteChangesApplied += OnRemoteChangesApplied;
    }

    /// <summary>Raised when the set of books changes locally or from sync.</summary>
    public event Action? Changed;

    /// <summary>All books that have not been deleted.</summary>
    public async Task<IReadOnlyList<Book>> GetAllAsync()
    {
        _cache ??= [.. (await _store.GetBooksAsync()).Where(b => !b.IsDeleted)];
        return _cache;
    }

    public async Task<Book?> GetAsync(string id)
    {
        var book = await _store.GetBookAsync(id);
        return book is { IsDeleted: false } ? book : null;
    }

    /// <summary>Adds or updates a book, stamping timestamps and trimming text.</summary>
    public async Task<Book> SaveAsync(Book book)
    {
        var now = _time.GetUtcNow();
        var existing = await _store.GetBookAsync(book.Id);
        var saved = book with
        {
            Title = book.Title.Trim(),
            Author = book.Author.Trim(),
            Isbn = Clean(book.Isbn),
            Publisher = Clean(book.Publisher),
            CoverUrl = Clean(book.CoverUrl),
            Notes = Clean(book.Notes),
            AddedUtc = existing?.AddedUtc ?? (book.AddedUtc == default ? now : book.AddedUtc),
            LastModified = now,
            IsDeleted = false,
        };

        await WriteAsync(saved);
        return saved;
    }

    public async Task DeleteAsync(string id)
    {
        if (await _store.GetBookAsync(id) is not { IsDeleted: false } book)
        {
            return;
        }

        await WriteAsync(book with { IsDeleted = true, LastModified = _time.GetUtcNow() });
    }

    private async Task WriteAsync(Book book)
    {
        await _store.PutBookAsync(book, OutboxEntry.ForBook(book.Id, book.LastModified));
        _cache = null;
        Changed?.Invoke();
        await _sync.NotifyLocalChangeAsync();
    }

    /// <summary>Drops the cached list after the local store was changed underneath (e.g. cleared).</summary>
    public void Invalidate()
    {
        _cache = null;
        Changed?.Invoke();
    }

    private void OnRemoteChangesApplied() => Invalidate();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void Dispose() => _sync.RemoteChangesApplied -= OnRemoteChangesApplied;
}

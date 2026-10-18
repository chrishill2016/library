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

    /// <summary>All books that have not been deleted, including wishlist books (see <see cref="Book.OnWishlist"/>).</summary>
    public async Task<IReadOnlyList<Book>> GetAllAsync()
    {
        _cache ??= [.. (await _store.GetBooksAsync()).Where(b => !b.IsDeleted)];
        return _cache;
    }

    /// <summary>A book already on the shelf with the same ISBN (10 or 13 form), if any.</summary>
    public async Task<Book?> FindByIsbnAsync(string isbn)
    {
        var wanted = Isbn.ToIsbn13(isbn);
        return wanted is null ? null : (await GetAllAsync()).FirstOrDefault(b => Isbn.ToIsbn13(b.Isbn) == wanted);
    }

    public async Task<Book?> GetAsync(string id)
    {
        var book = await _store.GetBookAsync(id);
        return book is { IsDeleted: false } ? book : null;
    }

    /// <summary>
    /// Adds or updates a book, stamping timestamps and trimming text. Pass <paramref name="syncNow"/>
    /// false when saving many books in a row, then call <see cref="SyncAsync"/> once.
    /// </summary>
    public async Task<Book> SaveAsync(Book book, bool syncNow = true)
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
            Synopsis = Clean(book.Synopsis),
            Genres = Bookshelf.Features.Genres.GenreCatalogue.Normalize(book.Genres),
            AddedUtc = existing?.AddedUtc ?? (book.AddedUtc == default ? now : book.AddedUtc),
            LastModified = now,
            IsDeleted = false,
        };

        await WriteAsync(saved, syncNow);
        return saved;
    }

    /// <summary>Uploads queued changes now (after saving with <c>syncNow: false</c>).</summary>
    public Task SyncAsync() => _sync.NotifyLocalChangeAsync();

    /// <summary>A wishlist book has been bought or borrowed: it joins the shelf.</summary>
    public async Task<Book?> MoveToShelfAsync(string id)
    {
        if (await GetAsync(id) is not { OnWishlist: true } book)
        {
            return null;
        }

        return await SaveAsync(book with { OnWishlist = false });
    }

    public async Task DeleteAsync(string id)
    {
        if (await _store.GetBookAsync(id) is not { IsDeleted: false } book)
        {
            return;
        }

        await WriteAsync(book with { IsDeleted = true, LastModified = _time.GetUtcNow() });
    }

    private async Task WriteAsync(Book book, bool syncNow = true)
    {
        await _store.PutBookAsync(book, OutboxEntry.ForBook(book.Id, book.LastModified));
        _cache = null;
        Changed?.Invoke();
        if (syncNow)
        {
            await _sync.NotifyLocalChangeAsync();
        }
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

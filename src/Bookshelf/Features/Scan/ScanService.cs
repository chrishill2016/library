using Bookshelf.Features.Books;
using Bookshelf.Features.Lookup;
using Bookshelf.Models;
using Bookshelf.Services.Browser;

namespace Bookshelf.Features.Scan;

public enum ScanStatus
{
    /// <summary>Added with details found online.</summary>
    Added,

    /// <summary>Added by ISBN only; details will be looked up when online.</summary>
    AddedPending,

    /// <summary>Added by ISBN only; not found online, needs details by hand.</summary>
    AddedNotFound,

    /// <summary>Confirm mode: details found, waiting for Add or Skip.</summary>
    Found,

    /// <summary>Confirm mode: nothing found (or offline), waiting for Add or Skip.</summary>
    NotFound,

    /// <summary>Already on the shelf; skipped unless another copy is added.</summary>
    Duplicate,

    Skipped,
    Undone,
}

/// <summary>One scanned barcode and what happened to it.</summary>
public sealed record ScanItem(
    Guid Id,
    string Isbn,
    ScanStatus Status,
    Book? Book = null,
    BookCandidate? Candidate = null,
    bool Offline = false)
{
    public bool IsAdded => Status is ScanStatus.Added or ScanStatus.AddedPending or ScanStatus.AddedNotFound;

    public bool AwaitingDecision => Status is ScanStatus.Found or ScanStatus.NotFound or ScanStatus.Duplicate;

    public string Title => Book?.Title ?? Candidate?.Title ?? $"ISBN {Isbn}";
}

/// <summary>
/// Turns a scanned barcode into a book on the shelf. In rapid mode books are
/// added straight away (with undo); in confirm mode the result waits for Add.
/// Duplicates are never added without asking.
/// </summary>
public sealed class ScanService(BookService books, IBookLookup lookup, IConnectivityService connectivity)
{
    /// <summary>Handles a barcode. Returns null for barcodes that aren't book ISBNs.</summary>
    public async Task<ScanItem?> HandleAsync(string code, bool confirmEach, CancellationToken cancellationToken = default)
    {
        // Accepts scanned ISBN-13 barcodes and typed ISBN-10s.
        if (!Isbn.IsValid(code))
        {
            return null;
        }

        var isbn13 = Isbn.ToIsbn13(code)!;
        if (await books.FindByIsbnAsync(isbn13) is { } existing)
        {
            return new ScanItem(Guid.NewGuid(), isbn13, ScanStatus.Duplicate, Book: existing);
        }

        var result = connectivity.IsOnline
            ? await lookup.LookupIsbnAsync(isbn13, cancellationToken)
            : LookupResult.Unavailable;
        var item = new ScanItem(
            Guid.NewGuid(),
            isbn13,
            result.Outcome == LookupOutcome.Found ? ScanStatus.Found : ScanStatus.NotFound,
            Candidate: result.Book,
            Offline: result.Outcome == LookupOutcome.Unavailable);

        return confirmEach ? item : await AddAsync(item, result.Outcome);
    }

    /// <summary>Adds a book waiting in confirm mode.</summary>
    public Task<ScanItem> AddAsync(ScanItem item) =>
        AddAsync(item, item.Candidate is not null ? LookupOutcome.Found
                     : item.Offline ? LookupOutcome.Unavailable
                     : LookupOutcome.NotFound);

    /// <summary>Adds a second copy of a book that's already on the shelf.</summary>
    public async Task<ScanItem> AddAnotherCopyAsync(ScanItem duplicate)
    {
        var existing = duplicate.Book ?? throw new InvalidOperationException("Not a duplicate.");
        var copy = await books.SaveAsync(existing with { Id = Book.NewId(), AddedUtc = default, Notes = null });
        return duplicate with { Status = ScanStatus.Added, Book = copy };
    }

    public async Task<ScanItem> UndoAsync(ScanItem item)
    {
        if (item.Book is { } book && item.IsAdded)
        {
            await books.DeleteAsync(book.Id);
        }

        return item with { Status = ScanStatus.Undone };
    }

    public static ScanItem Skip(ScanItem item) => item with { Status = ScanStatus.Skipped };

    /// <summary>A book saved by ISBN only, titled so it's easy to spot on the shelf.</summary>
    public static Book Placeholder(string isbn13, string lookupState) => new()
    {
        Id = Book.NewId(),
        Title = $"ISBN {isbn13}",
        Isbn = isbn13,
        LookupState = lookupState,
    };

    private async Task<ScanItem> AddAsync(ScanItem item, LookupOutcome outcome)
    {
        var (book, status) = outcome switch
        {
            LookupOutcome.Found => (item.Candidate!.ToNewBook(), ScanStatus.Added),
            LookupOutcome.NotFound => (Placeholder(item.Isbn, BookLookupState.NotFound), ScanStatus.AddedNotFound),
            _ => (Placeholder(item.Isbn, BookLookupState.Pending), ScanStatus.AddedPending),
        };

        var saved = await books.SaveAsync(book);
        return item with { Status = status, Book = saved };
    }
}

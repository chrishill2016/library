using Bookshelf.Features.Lookup;
using Bookshelf.Models;

namespace Bookshelf.Features.Books;

/// <summary>Mutable form model for adding or editing a <see cref="Book"/>.</summary>
public sealed class BookForm
{
    private Book? _original;

    /// <summary>Chosen up front so a photo can be taken before the first save.</summary>
    private readonly string _newId = Book.NewId();

    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string? Isbn { get; set; }
    public string? Publisher { get; set; }
    public int? PublishedYear { get; set; }
    public string? CoverUrl { get; set; }
    public string? Notes { get; set; }
    public string? PhotoId { get; set; }

    public bool IsNew => _original is null;

    public string Id => _original?.Id ?? _newId;

    /// <summary>The photo the book had when the form opened (to delete it if replaced).</summary>
    public string? OriginalPhotoId => _original?.PhotoId;

    public static BookForm From(Book book) => new()
    {
        _original = book,
        Title = book.Title,
        Author = book.Author,
        Isbn = book.Isbn,
        Publisher = book.Publisher,
        PublishedYear = book.PublishedYear,
        CoverUrl = book.CoverUrl,
        Notes = book.Notes,
        PhotoId = book.PhotoId,
    };

    public Book ToBook() => (_original ?? new Book { Id = _newId }) with
    {
        Title = Title,
        Author = Author,
        Isbn = Isbn,
        Publisher = Publisher,
        PublishedYear = PublishedYear,
        CoverUrl = CoverUrl,
        Notes = Notes,
        PhotoId = PhotoId,
        // Saving the form means a person has checked the details.
        LookupState = null,
    };

    /// <summary>Fills the form from an online lookup, keeping notes.</summary>
    public void Apply(BookCandidate found)
    {
        Title = found.Title;
        Author = found.Author;
        Isbn = found.Isbn ?? Isbn;
        Publisher = found.Publisher ?? Publisher;
        PublishedYear = found.PublishedYear ?? PublishedYear;
        CoverUrl = found.CoverUrl ?? CoverUrl;
    }

    /// <summary>A gentle warning (not a blocker): old books can have odd numbers.</summary>
    public string? IsbnWarning =>
        string.IsNullOrWhiteSpace(Isbn) || Books.Isbn.IsValid(Isbn)
            ? null
            : "That doesn't look like a valid ISBN-10 or ISBN-13. You can still save it.";
}

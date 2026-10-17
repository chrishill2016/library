using System.Text.RegularExpressions;
using Bookshelf.Features.Books;
using Bookshelf.Models;

namespace Bookshelf.Features.Lookup;

/// <summary>A book found online, ready to add to the shelf.</summary>
public sealed record BookCandidate(
    string Title,
    string Author,
    string? Isbn,
    string? Publisher,
    int? PublishedYear,
    string? CoverUrl,
    string Source)
{
    public Book ToNewBook() => new()
    {
        Id = Book.NewId(),
        Title = Title,
        Author = Author,
        Isbn = Isbn,
        Publisher = Publisher,
        PublishedYear = PublishedYear,
        CoverUrl = CoverUrl,
    };

    /// <summary>Fills this book's gaps from another source's result for the same ISBN.</summary>
    public BookCandidate FillFrom(BookCandidate other) => this with
    {
        Title = string.IsNullOrWhiteSpace(Title) ? other.Title : Title,
        Author = string.IsNullOrWhiteSpace(Author) ? other.Author : Author,
        Isbn = Isbn ?? other.Isbn,
        Publisher = Publisher ?? other.Publisher,
        PublishedYear = PublishedYear ?? other.PublishedYear,
        CoverUrl = CoverUrl ?? other.CoverUrl,
    };

    /// <summary>Key for spotting the same book from two sources in search results.</summary>
    internal string MatchKey =>
        Books.Isbn.ToIsbn13(Isbn) ?? $"{Simplify(Title)}|{Simplify(Author.Split('&')[0])}";

    private static string Simplify(string text) =>
        new([.. text.ToLowerInvariant().Where(char.IsLetterOrDigit)]);
}

public enum LookupOutcome
{
    Found,

    /// <summary>The sources answered, and none knows this ISBN.</summary>
    NotFound,

    /// <summary>Offline or the sources failed; worth trying again later.</summary>
    Unavailable,
}

public sealed record LookupResult(LookupOutcome Outcome, BookCandidate? Book = null)
{
    public static readonly LookupResult NotFound = new(LookupOutcome.NotFound);
    public static readonly LookupResult Unavailable = new(LookupOutcome.Unavailable);
}

/// <summary>One online catalogue (Google Books, Open Library).</summary>
public interface IBookSource
{
    string Name { get; }

    /// <summary>The book with this ISBN-13, or null when the source doesn't know it. Throws when unreachable.</summary>
    Task<BookCandidate?> FindByIsbnAsync(string isbn13, CancellationToken cancellationToken);

    /// <summary>Books matching free text (title and/or author). Throws when unreachable.</summary>
    Task<IReadOnlyList<BookCandidate>> SearchAsync(string query, CancellationToken cancellationToken);
}

internal static partial class LookupText
{
    /// <summary>First four-digit year in "1996", "1996-09-01" or "Sep 01, 1996".</summary>
    public static int? Year(string? date)
    {
        var match = date is null ? null : YearPattern().Match(date);
        return match is { Success: true } && int.TryParse(match.Value, out var year) ? year : null;
    }

    public static string Authors(IEnumerable<string>? names) =>
        string.Join(" & ", (names ?? []).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct());

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"\b(1[4-9]|20)\d{2}\b")]
    private static partial Regex YearPattern();
}

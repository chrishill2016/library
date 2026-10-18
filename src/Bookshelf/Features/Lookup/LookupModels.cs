using System.Net;
using System.Text.RegularExpressions;
using Bookshelf.Features.Books;
using Bookshelf.Features.Genres;
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
    /// <summary>The publisher's description, cleaned to plain text.</summary>
    public string? Synopsis { get; init; }

    /// <summary>Google Books categories ("Fiction / Mystery &amp; Detective / General").</summary>
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>Open Library subjects ("Detective and mystery stories"): many and noisy.</summary>
    public IReadOnlyList<string> Subjects { get; init; } = [];

    /// <summary>Up to three genre keys suggested from the categories and subjects.</summary>
    public IReadOnlyList<string> SuggestedGenres => GenreCatalogue.Suggest(Categories, Subjects);

    public Book ToNewBook() => new()
    {
        Id = Book.NewId(),
        Title = Title,
        Author = Author,
        Isbn = Isbn,
        Publisher = Publisher,
        PublishedYear = PublishedYear,
        CoverUrl = CoverUrl,
        Synopsis = Synopsis,
        Genres = SuggestedGenres,
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
        Synopsis = Synopsis ?? other.Synopsis,
        Categories = [.. Categories, .. other.Categories],
        Subjects = [.. Subjects, .. other.Subjects],
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

/// <summary>A source that can find a synopsis on its own, used when the main lookup found none.</summary>
public interface ISynopsisSource
{
    /// <summary>The description for this ISBN-13, or null. Throws when unreachable.</summary>
    Task<string?> FindSynopsisAsync(string isbn13, CancellationToken cancellationToken);
}

/// <summary>A source that answered with an error (quota, key restrictions…), as reported by the source.</summary>
public sealed class BookSourceException(string source, string message) : HttpRequestException($"{source}: {message}")
{
    public string Source { get; } = source;
}

public static partial class LookupText
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

    public const int MaxSynopsis = 4000;

    /// <summary>
    /// Plain text from a publisher description that may contain HTML (&lt;p&gt;, &lt;br&gt;, &lt;b&gt;)
    /// and entities. Paragraph breaks are kept; very long blurbs are cut at a sentence end.
    /// </summary>
    public static string? Synopsis(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var text = BlockTag().Replace(html, "\n\n");
        text = Tag().Replace(text, "");
        text = WebUtility.HtmlDecode(text).Replace('\u00a0', ' ');
        var paragraphs = text.Split('\n')
            .Select(line => Spaces().Replace(line, " ").Trim())
            .Where(line => line.Length > 0);
        text = string.Join("\n\n", paragraphs);
        if (text.Length == 0)
        {
            return null;
        }

        if (text.Length > MaxSynopsis)
        {
            var cut = text.LastIndexOf(". ", MaxSynopsis - 1, StringComparison.Ordinal);
            text = (cut > MaxSynopsis / 2 ? text[..(cut + 1)] : text[..(MaxSynopsis - 1)].TrimEnd()) + "…";
        }

        return text;
    }

    [GeneratedRegex(@"<\s*(br|/p|p|/div|div|/li|li)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockTag();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[ \t\r\f\v]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\b(1[4-9]|20)\d{2}\b")]
    private static partial Regex YearPattern();
}

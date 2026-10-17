using System.Globalization;
using System.Text;
using Bookshelf.Models;

namespace Bookshelf.Features.Books;

public enum BookSort
{
    Title,
    Author,
    RecentlyAdded,
    MyRating,
}

/// <summary>Shelf filter by the signed-in person's reading.</summary>
public enum ReadingFilter
{
    All,
    Reading,
    WantToRead,
    Read,
    NotRead,
}

/// <summary>In-memory search; plenty fast for a few thousand books.</summary>
public static class BookSearch
{
    private static readonly string[] LeadingArticles = ["the ", "a ", "an "];
    private static readonly string[] AuthorSeparators = ["&", ";", " and "];

    /// <summary>
    /// Matches books where every word of <paramref name="query"/> appears in the
    /// title, author or ISBN (ignoring case, accents and ISBN hyphens).
    /// </summary>
    public static IEnumerable<Book> Filter(IEnumerable<Book> books, string? query)
    {
        var terms = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
        {
            return books;
        }

        return books.Where(book =>
        {
            var haystack = Normalize($"{book.Title} {book.Author} {book.Isbn} {Isbn.Normalize(book.Isbn)}");
            return terms.All(haystack.Contains);
        });
    }

    /// <summary>
    /// Sorts the shelf. <paramref name="mine"/> gives the signed-in person's reading of a book
    /// (for <see cref="BookSort.MyRating"/>: highest first, unrated last, then by title).
    /// </summary>
    public static IEnumerable<Book> Sort(IEnumerable<Book> books, BookSort sort, Func<Book, Reading?>? mine = null) => sort switch
    {
        BookSort.Author => books.OrderBy(b => AuthorSortKey(b.Author), StringComparer.OrdinalIgnoreCase)
                                .ThenBy(b => TitleSortKey(b.Title), StringComparer.OrdinalIgnoreCase),
        BookSort.RecentlyAdded => books.OrderByDescending(b => b.AddedUtc),
        BookSort.MyRating => books.OrderByDescending(b => mine?.Invoke(b)?.Rating ?? 0)
                                  .ThenBy(b => TitleSortKey(b.Title), StringComparer.OrdinalIgnoreCase),
        _ => books.OrderBy(b => TitleSortKey(b.Title), StringComparer.OrdinalIgnoreCase),
    };

    /// <summary>
    /// Keeps books matching the signed-in person's reading. "Read" means marked read or with a read date;
    /// "Not read" is everything else (including want-to-read and currently reading).
    /// </summary>
    public static IEnumerable<Book> FilterByReading(IEnumerable<Book> books, ReadingFilter filter, Func<Book, Reading?> mine) => filter switch
    {
        ReadingFilter.Reading => books.Where(b => mine(b)?.Status == ReadingStatus.Reading),
        ReadingFilter.WantToRead => books.Where(b => mine(b)?.Status == ReadingStatus.WantToRead),
        ReadingFilter.Read => books.Where(b => mine(b)?.HasRead == true),
        ReadingFilter.NotRead => books.Where(b => mine(b)?.HasRead != true),
        _ => books,
    };

    /// <summary>Library-style title key: "The Hobbit" files under H.</summary>
    public static string TitleSortKey(string title)
    {
        var trimmed = title.Trim();
        foreach (var article in LeadingArticles)
        {
            if (trimmed.StartsWith(article, StringComparison.OrdinalIgnoreCase) && trimmed.Length > article.Length)
            {
                return trimmed[article.Length..].TrimStart();
            }
        }

        return trimmed;
    }

    /// <summary>
    /// Files by the first author's surname: "J. R. R. Tolkien" → "Tolkien J. R. R.".
    /// "Tolkien, J. R. R." is already surname-first and is kept as is.
    /// </summary>
    public static string AuthorSortKey(string author)
    {
        var first = author.Split(AuthorSeparators, 2, StringSplitOptions.None)[0].Trim();
        var comma = first.IndexOf(',');
        if (comma > 0)
        {
            var beforeComma = first[..comma].Trim();
            if (!beforeComma.Contains(' '))
            {
                return first;
            }

            // "Neil Gaiman, Terry Pratchett": a list of names, not surname-first.
            first = beforeComma;
        }

        var parts = first.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= 1 ? first : $"{parts[^1]} {string.Join(' ', parts[..^1])}";
    }

    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}

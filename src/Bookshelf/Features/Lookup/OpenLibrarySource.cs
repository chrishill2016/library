using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bookshelf.Features.Books;

namespace Bookshelf.Features.Lookup;

/// <summary>Open Library: free, no key, and a good source of cover art.</summary>
public sealed class OpenLibrarySource(HttpClient http) : IBookSource
{
    private const string BooksEndpoint = "https://openlibrary.org/api/books";
    private const string SearchEndpoint = "https://openlibrary.org/search.json";
    private const string SearchFields = "key,title,author_name,first_publish_year,isbn,cover_i,publisher";

    public string Name => "Open Library";

    public async Task<BookCandidate?> FindByIsbnAsync(string isbn13, CancellationToken cancellationToken)
    {
        var response = await http.GetFromJsonAsync<Dictionary<string, Edition>>(
            $"{BooksEndpoint}?bibkeys=ISBN:{isbn13}&format=json&jscmd=data", cancellationToken);
        return response is not null && response.TryGetValue($"ISBN:{isbn13}", out var edition)
            ? ToCandidate(edition, isbn13)
            : null;
    }

    public async Task<IReadOnlyList<BookCandidate>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var response = await http.GetFromJsonAsync<SearchResponse>(
            $"{SearchEndpoint}?q={Uri.EscapeDataString(query)}&limit=15&fields={SearchFields}", cancellationToken);
        return [.. (response?.Docs ?? []).Select(ToCandidate).OfType<BookCandidate>()];
    }

    internal static BookCandidate? ToCandidate(Edition edition, string isbn13)
    {
        if (string.IsNullOrWhiteSpace(edition.Title))
        {
            return null;
        }

        return new BookCandidate(
            edition.Title.Trim(),
            LookupText.Authors(edition.Authors?.Select(a => a.Name ?? "")),
            isbn13,
            LookupText.Clean(edition.Publishers?.FirstOrDefault()?.Name),
            LookupText.Year(edition.PublishDate),
            edition.Cover?.Medium ?? edition.Cover?.Large ?? edition.Cover?.Small,
            "Open Library");
    }

    internal static BookCandidate? ToCandidate(SearchDoc doc)
    {
        if (string.IsNullOrWhiteSpace(doc.Title))
        {
            return null;
        }

        // Prefer an ISBN-13 from the list; search results mix editions.
        var isbn = doc.Isbn?.Select(Isbn.ToIsbn13).FirstOrDefault(i => i is not null);
        return new BookCandidate(
            doc.Title.Trim(),
            LookupText.Authors(doc.AuthorName),
            isbn,
            LookupText.Clean(doc.Publisher?.FirstOrDefault()),
            doc.FirstPublishYear,
            doc.CoverId is { } id ? $"https://covers.openlibrary.org/b/id/{id}-M.jpg" : null,
            "Open Library");
    }

    internal sealed record Edition(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("authors")] NamedItem[]? Authors,
        [property: JsonPropertyName("publishers")] NamedItem[]? Publishers,
        [property: JsonPropertyName("publish_date")] string? PublishDate,
        [property: JsonPropertyName("cover")] Cover? Cover);

    internal sealed record NamedItem([property: JsonPropertyName("name")] string? Name);

    internal sealed record Cover(
        [property: JsonPropertyName("small")] string? Small,
        [property: JsonPropertyName("medium")] string? Medium,
        [property: JsonPropertyName("large")] string? Large);

    internal sealed record SearchResponse([property: JsonPropertyName("docs")] SearchDoc[]? Docs);

    internal sealed record SearchDoc(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("author_name")] string[]? AuthorName,
        [property: JsonPropertyName("first_publish_year")] int? FirstPublishYear,
        [property: JsonPropertyName("isbn")] string[]? Isbn,
        [property: JsonPropertyName("cover_i")] long? CoverId,
        [property: JsonPropertyName("publisher")] string[]? Publisher);
}

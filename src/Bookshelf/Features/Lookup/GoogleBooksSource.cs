using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bookshelf.Features.Books;

namespace Bookshelf.Features.Lookup;

/// <summary>
/// Google Books volumes API. Good coverage of UK editions. Uses an API key when
/// configured (BookLookup:GoogleBooksApiKey), since keyless quota is unreliable.
/// </summary>
public sealed class GoogleBooksSource(HttpClient http, LookupOptions options) : IBookSource
{
    private const string Endpoint = "https://www.googleapis.com/books/v1/volumes";

    public string Name => "Google Books";

    public async Task<BookCandidate?> FindByIsbnAsync(string isbn13, CancellationToken cancellationToken)
    {
        var response = await GetAsync($"q=isbn:{isbn13}&maxResults=5&printType=books", cancellationToken);
        return response?.Items?
            .Select(ToCandidate)
            .OfType<BookCandidate>()
            .OrderByDescending(c => Isbn.ToIsbn13(c.Isbn) == isbn13)
            .FirstOrDefault() is { } found
                ? found with { Isbn = isbn13 }
                : null;
    }

    public async Task<IReadOnlyList<BookCandidate>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var response = await GetAsync($"q={Uri.EscapeDataString(query)}&maxResults=15&printType=books", cancellationToken);
        return [.. (response?.Items ?? []).Select(ToCandidate).OfType<BookCandidate>()];
    }

    private async Task<VolumesResponse?> GetAsync(string query, CancellationToken cancellationToken)
    {
        var key = string.IsNullOrWhiteSpace(options.GoogleBooksApiKey) ? "" : $"&key={Uri.EscapeDataString(options.GoogleBooksApiKey)}";
        return await http.GetFromJsonAsync<VolumesResponse>($"{Endpoint}?{query}{key}", cancellationToken);
    }

    internal static BookCandidate? ToCandidate(Volume volume)
    {
        var info = volume.VolumeInfo;
        if (info is null || string.IsNullOrWhiteSpace(info.Title))
        {
            return null;
        }

        var ids = info.IndustryIdentifiers ?? [];
        var isbn = ids.FirstOrDefault(i => i.Type == "ISBN_13")?.Identifier
            ?? Isbn.ToIsbn13(ids.FirstOrDefault(i => i.Type == "ISBN_10")?.Identifier);

        return new BookCandidate(
            info.Title.Trim(),
            LookupText.Authors(info.Authors),
            isbn,
            LookupText.Clean(info.Publisher),
            LookupText.Year(info.PublishedDate),
            CoverUrl(info.ImageLinks),
            "Google Books")
        {
            Synopsis = LookupText.Synopsis(info.Description),
            Categories = [.. (info.Categories ?? []).Where(c => !string.IsNullOrWhiteSpace(c))],
        };
    }

    /// <summary>Google returns http links with a page-curl effect; use https and a flat cover.</summary>
    internal static string? CoverUrl(ImageLinks? links)
    {
        var url = links?.Thumbnail ?? links?.SmallThumbnail;
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return url.Replace("http://", "https://", StringComparison.OrdinalIgnoreCase)
                  .Replace("&edge=curl", "", StringComparison.OrdinalIgnoreCase);
    }

    internal sealed record VolumesResponse([property: JsonPropertyName("items")] Volume[]? Items);

    internal sealed record Volume([property: JsonPropertyName("volumeInfo")] VolumeInfo? VolumeInfo);

    internal sealed record VolumeInfo(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("authors")] string[]? Authors,
        [property: JsonPropertyName("publisher")] string? Publisher,
        [property: JsonPropertyName("publishedDate")] string? PublishedDate,
        [property: JsonPropertyName("industryIdentifiers")] IndustryIdentifier[]? IndustryIdentifiers,
        [property: JsonPropertyName("imageLinks")] ImageLinks? ImageLinks,
        [property: JsonPropertyName("description")] string? Description = null,
        [property: JsonPropertyName("categories")] string[]? Categories = null);

    internal sealed record IndustryIdentifier(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("identifier")] string? Identifier);

    internal sealed record ImageLinks(
        [property: JsonPropertyName("smallThumbnail")] string? SmallThumbnail,
        [property: JsonPropertyName("thumbnail")] string? Thumbnail);
}

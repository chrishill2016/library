using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bookshelf.Features.Books;

namespace Bookshelf.Features.Lookup;

/// <summary>Open Library: free, no key, and a good source of cover art, subjects and (on the work) descriptions.</summary>
public sealed class OpenLibrarySource(HttpClient http) : IBookSource, ISynopsisSource
{
    private const string Site = "https://openlibrary.org";

    private const string BooksEndpoint = "https://openlibrary.org/api/books";
    private const string SearchEndpoint = "https://openlibrary.org/search.json";
    private const string SearchFields = "key,title,author_name,first_publish_year,isbn,cover_i,publisher,subject";

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

    /// <summary>
    /// Descriptions live on the work ("The Hobbit"), not the edition: /isbn/{isbn}.json gives the
    /// edition and its work key, and /works/{id}.json has the description (a string or {"value": …}).
    /// </summary>
    public async Task<string?> FindSynopsisAsync(string isbn13, CancellationToken cancellationToken)
    {
        using var editionResponse = await http.GetAsync($"{Site}/isbn/{isbn13}.json", cancellationToken);
        if (editionResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        editionResponse.EnsureSuccessStatusCode();
        var edition = await editionResponse.Content.ReadFromJsonAsync<EditionRecord>(cancellationToken);
        var description = LookupText.Synopsis(edition?.Description?.Text);
        if (description is not null)
        {
            return description;
        }

        var workKey = edition?.Works?.FirstOrDefault()?.Key;
        if (workKey is null || !workKey.StartsWith("/works/", StringComparison.Ordinal))
        {
            return null;
        }

        var work = await http.GetFromJsonAsync<WorkRecord>($"{Site}{workKey}.json", cancellationToken);
        return LookupText.Synopsis(work?.Description?.Text);
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
            "Open Library")
        {
            Subjects = [.. (edition.Subjects ?? []).Select(s => s.Name).OfType<string>()],
        };
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
            "Open Library")
        {
            Subjects = [.. (doc.Subject ?? []).Take(40)],
        };
    }

    internal sealed record Edition(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("authors")] NamedItem[]? Authors,
        [property: JsonPropertyName("publishers")] NamedItem[]? Publishers,
        [property: JsonPropertyName("publish_date")] string? PublishDate,
        [property: JsonPropertyName("cover")] Cover? Cover,
        [property: JsonPropertyName("subjects")] NamedItem[]? Subjects = null);

    internal sealed record NamedItem([property: JsonPropertyName("name")] string? Name);

    internal sealed record Cover(
        [property: JsonPropertyName("small")] string? Small,
        [property: JsonPropertyName("medium")] string? Medium,
        [property: JsonPropertyName("large")] string? Large);

    internal sealed record EditionRecord(
        [property: JsonPropertyName("works")] KeyRef[]? Works,
        [property: JsonPropertyName("description")] TextValue? Description);

    internal sealed record WorkRecord([property: JsonPropertyName("description")] TextValue? Description);

    internal sealed record KeyRef([property: JsonPropertyName("key")] string? Key);

    /// <summary>Open Library text is either a plain string or {"type": "/type/text", "value": "…"}.</summary>
    [JsonConverter(typeof(TextValueConverter))]
    internal sealed record TextValue(string? Text);

    internal sealed class TextValueConverter : JsonConverter<TextValue>
    {
        public override TextValue? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case System.Text.Json.JsonTokenType.String:
                    return new TextValue(reader.GetString());
                case System.Text.Json.JsonTokenType.StartObject:
                    using (var doc = System.Text.Json.JsonDocument.ParseValue(ref reader))
                    {
                        return new TextValue(doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
                            ? value.GetString()
                            : null);
                    }

                default:
                    reader.Skip();
                    return null;
            }
        }

        public override void Write(System.Text.Json.Utf8JsonWriter writer, TextValue value, System.Text.Json.JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Text);
    }

    internal sealed record SearchResponse([property: JsonPropertyName("docs")] SearchDoc[]? Docs);

    internal sealed record SearchDoc(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("author_name")] string[]? AuthorName,
        [property: JsonPropertyName("first_publish_year")] int? FirstPublishYear,
        [property: JsonPropertyName("isbn")] string[]? Isbn,
        [property: JsonPropertyName("cover_i")] long? CoverId,
        [property: JsonPropertyName("publisher")] string[]? Publisher,
        [property: JsonPropertyName("subject")] string[]? Subject = null);
}

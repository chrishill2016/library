using System.Net;
using Bookshelf.Features.Lookup;
using Bookshelf.Tests.Fakes;

namespace Bookshelf.Tests.Lookup;

public class BookLookupServiceTests
{
    private readonly StubHttpHandler _http = new();

    private BookLookupService CreateSut()
    {
        var client = new HttpClient(_http);
        return new BookLookupService([new GoogleBooksSource(client, new LookupOptions(null)), new OpenLibrarySource(client)]);
    }

    [Fact]
    public async Task Google_details_win_and_open_library_fills_missing_cover_and_publisher()
    {
        _http.Json("isbn:9780261102217", "google-isbn-nocover.json")
             .Json("bibkeys=ISBN:9780261102217", "openlibrary-isbn-hobbit.json");

        var result = await CreateSut().LookupIsbnAsync("9780261102217");

        Assert.Equal(LookupOutcome.Found, result.Outcome);
        Assert.Equal("The Hobbit", result.Book!.Title);
        Assert.Equal("J. R. R. Tolkien", result.Book.Author);
        Assert.Equal("HarperCollins", result.Book.Publisher);
        Assert.Equal("https://covers.openlibrary.org/b/id/8406786-M.jpg", result.Book.CoverUrl);
    }

    [Fact]
    public async Task Merges_sources_for_an_isbn()
    {
        _http.Json("isbn:9780261102217", "google-isbn-hobbit.json")
             .Json("bibkeys=ISBN:9780261102217", "openlibrary-isbn-hobbit.json");

        var result = await CreateSut().LookupIsbnAsync("0-261-10221-4");

        Assert.Equal(LookupOutcome.Found, result.Outcome);
        Assert.Equal("The Hobbit", result.Book!.Title);
        Assert.Equal("9780261102217", result.Book.Isbn);
        Assert.StartsWith("https://books.google.com/", result.Book.CoverUrl);
    }

    [Fact]
    public async Task Falls_back_to_open_library_when_google_is_over_quota()
    {
        _http.Status("googleapis.com", HttpStatusCode.TooManyRequests)
             .Json("bibkeys=ISBN:9780261102217", "openlibrary-isbn-hobbit.json");

        var result = await CreateSut().LookupIsbnAsync("9780261102217");

        Assert.Equal(LookupOutcome.Found, result.Outcome);
        Assert.Equal("The hobbit", result.Book!.Title);
        Assert.Equal("https://covers.openlibrary.org/b/id/8406786-M.jpg", result.Book.CoverUrl);
    }

    [Fact]
    public async Task Not_found_only_when_every_source_answered()
    {
        _http.Json("googleapis.com", "google-empty.json").Json("openlibrary.org", "openlibrary-empty.json");

        Assert.Equal(LookupOutcome.NotFound, (await CreateSut().LookupIsbnAsync("9780261102217")).Outcome);
    }

    [Fact]
    public async Task Unavailable_when_a_source_fails_and_none_found()
    {
        _http.Json("googleapis.com", "google-empty.json").Throws("openlibrary.org");

        Assert.Equal(LookupOutcome.Unavailable, (await CreateSut().LookupIsbnAsync("9780261102217")).Outcome);
    }

    [Fact]
    public async Task Invalid_isbn_is_not_found_without_calling_out()
    {
        var result = await CreateSut().LookupIsbnAsync("12345");

        Assert.Equal(LookupOutcome.NotFound, result.Outcome);
        Assert.Empty(_http.Requests);
    }

    [Fact]
    public async Task Search_merges_duplicates_across_sources_keeping_google_first()
    {
        _http.Json("googleapis.com", "google-search-emma.json").Json("search.json", "openlibrary-search-emma.json");

        var results = await CreateSut().SearchAsync("emma");

        Assert.Equal(["Emma", "Emma: A Study Guide", "Emma and the Vampires"], results.Select(r => r.Title));
        // Same ISBN from both: Google's entry kept, Open Library's year filled the gap only if missing.
        Assert.Equal(2003, results[0].PublishedYear);
        Assert.Equal("Penguin UK", results[0].Publisher);
    }

    [Fact]
    public async Task Search_works_when_one_source_is_down()
    {
        _http.Throws("googleapis.com").Json("search.json", "openlibrary-search-emma.json");

        Assert.Equal(2, (await CreateSut().SearchAsync("emma")).Count);
    }

    [Fact]
    public async Task Blank_search_returns_nothing() =>
        Assert.Empty(await CreateSut().SearchAsync("  "));
}

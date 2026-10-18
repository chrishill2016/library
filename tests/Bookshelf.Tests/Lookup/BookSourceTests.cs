using System.Net;
using Bookshelf.Features.Lookup;
using Bookshelf.Tests.Fakes;

namespace Bookshelf.Tests.Lookup;

public class BookSourceTests
{
    private readonly StubHttpHandler _http = new();

    private GoogleBooksSource Google(string? key = null) => new(new HttpClient(_http), new LookupOptions(key));

    private OpenLibrarySource OpenLibrary() => new(new HttpClient(_http));

    [Fact]
    public async Task Google_isbn_lookup_parses_details_and_cleans_cover_url()
    {
        _http.Json("isbn:9780261102217", "google-isbn-hobbit.json");

        var book = await Google().FindByIsbnAsync("9780261102217", default);

        Assert.NotNull(book);
        Assert.Equal("The Hobbit", book.Title);
        Assert.Equal("J. R. R. Tolkien", book.Author);
        Assert.Equal("9780261102217", book.Isbn);
        Assert.Equal("HarperCollins UK", book.Publisher);
        Assert.Equal(1996, book.PublishedYear);
        Assert.Equal("https://books.google.com/books/content?id=pD6arNyKyi8C&printsec=frontcover&img=1&zoom=1&source=gbs_api", book.CoverUrl);
    }

    [Fact]
    public async Task Google_uses_api_key_when_configured()
    {
        _http.Json("isbn:", "google-empty.json");

        await Google("abc 123").FindByIsbnAsync("9780261102217", default);

        Assert.EndsWith("&key=abc%20123", _http.Requests.Single());
    }

    [Fact]
    public async Task Google_returns_null_when_nothing_found()
    {
        _http.Json("isbn:", "google-empty.json");

        Assert.Null(await Google().FindByIsbnAsync("9780261102217", default));
    }

    [Fact]
    public async Task Google_quota_errors_throw_so_lookup_can_fall_back()
    {
        _http.Status("isbn:", HttpStatusCode.TooManyRequests);

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => Google().FindByIsbnAsync("9780261102217", default));
    }

    [Fact]
    public async Task Google_search_converts_isbn10_parses_odd_dates_and_skips_untitled()
    {
        _http.Json("q=jane%20austen%20emma", "google-search-emma.json");

        var results = await Google().SearchAsync("jane austen emma", default);

        Assert.Equal(2, results.Count);
        Assert.Equal("9780141439587", results[0].Isbn);
        Assert.Equal(2010, results[1].PublishedYear);
        Assert.Null(results[1].CoverUrl);
    }

    [Fact]
    public async Task Open_library_isbn_lookup_parses_details_and_cover()
    {
        _http.Json("bibkeys=ISBN:9780261102217", "openlibrary-isbn-hobbit.json");

        var book = await OpenLibrary().FindByIsbnAsync("9780261102217", default);

        Assert.NotNull(book);
        Assert.Equal("The hobbit", book.Title);
        Assert.Equal("J.R.R. Tolkien", book.Author);
        Assert.Equal("HarperCollins", book.Publisher);
        Assert.Equal(1996, book.PublishedYear);
        Assert.Equal("https://covers.openlibrary.org/b/id/8406786-M.jpg", book.CoverUrl);
    }

    [Fact]
    public async Task Open_library_returns_null_for_unknown_isbn()
    {
        _http.Json("bibkeys=", "openlibrary-empty.json");

        Assert.Null(await OpenLibrary().FindByIsbnAsync("9780261102217", default));
    }

    [Fact]
    public async Task Open_library_search_builds_cover_urls_and_joins_authors()
    {
        _http.Json("search.json", "openlibrary-search-emma.json");

        var results = await OpenLibrary().SearchAsync("emma", default);

        Assert.Equal(2, results.Count);
        Assert.Equal("9780141439587", results[0].Isbn);
        Assert.Equal("https://covers.openlibrary.org/b/id/9278312-M.jpg", results[0].CoverUrl);
        Assert.Equal(1815, results[0].PublishedYear);
        Assert.Equal("Wayne Josephson & Jane Austen", results[1].Author);
        Assert.Null(results[1].Isbn);
    }
}

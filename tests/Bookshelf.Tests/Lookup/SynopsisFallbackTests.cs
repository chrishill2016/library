using System.Net;
using Bookshelf.Features.Lookup;
using Bookshelf.Tests.Fakes;

namespace Bookshelf.Tests.Lookup;

public class SynopsisFallbackTests
{
    private const string Hobbit = "9780261102217";
    private readonly StubHttpHandler _http = new();

    private BookLookupService CreateSut()
    {
        var client = new HttpClient(_http);
        return new BookLookupService([new GoogleBooksSource(client, new LookupOptions("key")), new OpenLibrarySource(client)]);
    }

    private StubHttpHandler OpenLibraryWithWork(string workFixture = "openlibrary-work-hobbit.json") =>
        _http.Json("bibkeys=ISBN:9780261102217", "openlibrary-isbn-hobbit.json")
             .Json($"/isbn/{Hobbit}.json", "openlibrary-edition-hobbit.json")
             .Json("/works/OL262758W.json", workFixture);

    [Fact]
    public async Task Uses_the_open_library_work_description_when_google_has_none()
    {
        _http.Json("isbn:9780261102217", "google-isbn-nocover.json");
        OpenLibraryWithWork();

        var result = await CreateSut().LookupIsbnAsync(Hobbit);

        Assert.Equal(
            "Bilbo Baggins is a hobbit who enjoys a comfortable, unambitious life.\n\nGandalf and thirteen dwarves sweep him off on an adventure.",
            result.Book!.Synopsis);
    }

    [Fact]
    public async Task Plain_string_descriptions_work_too()
    {
        _http.Json("isbn:9780261102217", "google-empty.json");
        OpenLibraryWithWork("openlibrary-work-plain.json");

        var result = await CreateSut().LookupIsbnAsync(Hobbit);

        Assert.Equal("Emma Woodhouse is handsome, clever and rich.", result.Book!.Synopsis);
    }

    [Fact]
    public async Task Skips_open_library_descriptions_when_google_has_one()
    {
        _http.Json("isbn:9780261102217", "google-isbn-hobbit.json");
        OpenLibraryWithWork();

        var result = await CreateSut().LookupIsbnAsync(Hobbit);

        Assert.StartsWith("Bilbo Baggins is a hobbit who enjoys a comfortable, unambitious life.\n\nBut", result.Book!.Synopsis);
        Assert.DoesNotContain(_http.Requests, r => r.Contains("/works/"));
    }

    [Fact]
    public async Task Google_quota_errors_are_reported_and_open_library_still_answers()
    {
        _http.On("isbn:9780261102217", HttpStatusCode.TooManyRequests, "google-error-quota.json");
        OpenLibraryWithWork();
        var sut = CreateSut();

        var result = await sut.LookupIsbnAsync(Hobbit);

        Assert.Equal(LookupOutcome.Found, result.Outcome);
        Assert.StartsWith("Bilbo Baggins", result.Book!.Synopsis);
        Assert.StartsWith("Quota exceeded", sut.Problems["Google Books"]);
    }

    [Fact]
    public async Task Missing_edition_or_work_just_means_no_synopsis()
    {
        _http.Json("isbn:9780261102217", "google-empty.json")
             .Json("bibkeys=ISBN:9780261102217", "openlibrary-isbn-hobbit.json")
             .Status($"/isbn/{Hobbit}.json", HttpStatusCode.NotFound);

        var result = await CreateSut().LookupIsbnAsync(Hobbit);

        Assert.Equal(LookupOutcome.Found, result.Outcome);
        Assert.Null(result.Book!.Synopsis);
    }
}

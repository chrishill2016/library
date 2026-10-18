using Bookshelf.Features.Lookup;
using Bookshelf.Tests.Fakes;

namespace Bookshelf.Tests.Lookup;

public class SynopsisAndGenreLookupTests
{
    private readonly StubHttpHandler _http = new();

    private BookLookupService CreateSut()
    {
        var client = new HttpClient(_http);
        return new BookLookupService([new GoogleBooksSource(client, new LookupOptions(null)), new OpenLibrarySource(client)]);
    }

    [Fact]
    public async Task Google_description_becomes_a_plain_text_synopsis()
    {
        _http.Json("isbn:9780261102217", "google-isbn-hobbit.json");

        var book = await new GoogleBooksSource(new HttpClient(_http), new LookupOptions(null)).FindByIsbnAsync("9780261102217", default);

        Assert.Equal(
            "Bilbo Baggins is a hobbit who enjoys a comfortable, unambitious life.\n\n" +
            "But his contentment is disturbed when Gandalf the wizard & a company of dwarves arrive on his doorstep one day.\n\nA classic.",
            book!.Synopsis);
        Assert.Equal(["Juvenile Fiction / Fantasy & Magic"], book.Categories);
    }

    [Fact]
    public async Task Merged_lookup_combines_google_categories_and_open_library_subjects_into_genres()
    {
        _http.Json("isbn:9780261102217", "google-isbn-hobbit.json")
             .Json("bibkeys=ISBN:9780261102217", "openlibrary-isbn-hobbit.json");

        var result = await CreateSut().LookupIsbnAsync("9780261102217");

        Assert.Equal(4, result.Book!.Subjects.Count);
        Assert.Equal(["children", "fantasy"], result.Book.SuggestedGenres.Order());
        var book = result.Book.ToNewBook();
        Assert.StartsWith("Bilbo Baggins", book.Synopsis);
        Assert.Equal(result.Book.SuggestedGenres, book.Genres);
    }

    [Fact]
    public async Task Open_library_search_subjects_suggest_genres()
    {
        _http.Json("search.json", "openlibrary-search-emma.json");

        var results = await new OpenLibrarySource(new HttpClient(_http)).SearchAsync("emma", default);

        Assert.Contains("subject", _http.Requests.Single());
        Assert.Equal(["romance", "classics"], results[0].SuggestedGenres);
        Assert.Empty(results[1].SuggestedGenres);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("<p>  </p>", null)]
    [InlineData("Plain text.", "Plain text.")]
    [InlineData("One&#39;s <i>own</i>   room", "One's own room")]
    [InlineData("Line one<br/>Line two", "Line one\n\nLine two")]
    public void Cleans_descriptions(string? html, string? expected) =>
        Assert.Equal(expected, LookupText.Synopsis(html));

    [Fact]
    public void Long_descriptions_are_cut_at_a_sentence()
    {
        var sentence = "This is a sentence about the book. ";
        var synopsis = LookupText.Synopsis(string.Concat(Enumerable.Repeat(sentence, 200)))!;

        Assert.True(synopsis.Length <= LookupText.MaxSynopsis + 1);
        Assert.EndsWith("book.…", synopsis);
    }

    [Fact]
    public void Filling_from_another_source_keeps_the_first_synopsis()
    {
        var google = new BookCandidate("Emma", "Jane Austen", null, null, null, null, "G") { Synopsis = "Google's", Categories = ["Fiction / Classics"] };
        var openLibrary = new BookCandidate("Emma", "", null, null, null, null, "OL") { Synopsis = "OL's", Subjects = ["Love stories"] };

        var merged = google.FillFrom(openLibrary);

        Assert.Equal("Google's", merged.Synopsis);
        Assert.Equal(["Fiction / Classics"], merged.Categories);
        Assert.Equal(["Love stories"], merged.Subjects);
    }
}

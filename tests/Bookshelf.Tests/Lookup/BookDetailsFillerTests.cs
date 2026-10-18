using Bookshelf.Features.Books;
using Bookshelf.Features.Lookup;
using Bookshelf.Models;
using Bookshelf.Services.Sync;
using Bookshelf.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Bookshelf.Tests.Lookup;

public class BookDetailsFillerTests
{
    private const string Hobbit = "9780261102217";
    private const string Emma = "9780141439587";

    private readonly InMemoryLocalStore _store = new();
    private readonly FakeRemoteStore _remote = new();
    private readonly FakeConnectivity _connectivity = new();
    private readonly FakeBookLookup _lookup = new();
    private readonly BookService _books;
    private readonly BookDetailsFiller _sut;

    public BookDetailsFillerTests()
    {
        var time = new FakeTimeProvider(TestBooks.Epoch);
        var sync = new SyncService(_store, _remote, _connectivity, time);
        _books = new BookService(_store, sync, time);
        _sut = new BookDetailsFiller(_books, _lookup, _connectivity) { Pause = TimeSpan.Zero };
        _lookup.Known[Hobbit] = new BookCandidate("The Hobbit", "J. R. R. Tolkien", Hobbit, null, 1937, null, "Fake")
        {
            Synopsis = "Bilbo goes there and back again.",
            Categories = ["Juvenile Fiction / Fantasy & Magic"],
        };
    }

    [Fact]
    public async Task Fills_missing_synopsis_and_genres_by_isbn()
    {
        await _books.SaveAsync(TestBooks.Make("h", "The Hobbit") with { Isbn = Hobbit });

        await _sut.RunAsync();

        Assert.Equal("Bilbo goes there and back again.", _store.Books["h"].Synopsis);
        Assert.Equal(["fantasy", "children"], _store.Books["h"].Genres);
        Assert.Equal((1, 1, 1), (_sut.Checked, _sut.Filled, _sut.Total));
        Assert.Equal(["fantasy", "children"], _remote.Books["h"].Genres);
    }

    [Fact]
    public async Task Never_overwrites_what_is_there()
    {
        await _books.SaveAsync(TestBooks.Make("h", "The Hobbit") with { Isbn = Hobbit, Synopsis = "My own words." });

        await _sut.RunAsync();

        Assert.Equal("My own words.", _store.Books["h"].Synopsis);
        Assert.Equal(["fantasy", "children"], _store.Books["h"].Genres);
    }

    [Fact]
    public async Task Books_without_isbn_need_an_exact_title_and_author_match()
    {
        _lookup.SearchResults.Add(new BookCandidate("Emma: A Graphic Novel", "Jane Austen", null, null, null, null, "Fake") { Synopsis = "Wrong book" });
        _lookup.SearchResults.Add(new BookCandidate("Emma", "Jane Austen", null, null, null, null, "Fake") { Synopsis = "Right book", Categories = ["Fiction / Classics"] });
        await _books.SaveAsync(TestBooks.Make("e", "Emma", "Jane Austen"));
        await _books.SaveAsync(TestBooks.Make("x", "Emma", "Someone Else"));

        await _sut.RunAsync();

        Assert.Equal("Right book", _store.Books["e"].Synopsis);
        Assert.Equal(["classics"], _store.Books["e"].Genres);
        Assert.Null(_store.Books["x"].Synopsis);
    }

    [Fact]
    public async Task Stops_when_the_sources_are_unreachable_and_skips_pending_books()
    {
        _lookup.Unavailable = true;
        await _books.SaveAsync(TestBooks.Make("h", "The Hobbit") with { Isbn = Hobbit });
        await _books.SaveAsync(TestBooks.Make("p", "ISBN only") with { Isbn = Emma, LookupState = BookLookupState.Pending });

        Assert.Equal(1, await _sut.CountMissingAsync());
        await _sut.RunAsync();

        Assert.Equal(0, _sut.Filled);
        Assert.Contains("Couldn't reach", _sut.StoppedReason);
        Assert.False(_sut.IsRunning);
    }

    [Fact]
    public async Task Syncs_in_batches_not_after_every_book()
    {
        for (var i = 0; i < 30; i++)
        {
            await _books.SaveAsync(TestBooks.Make($"h{i}", "The Hobbit") with { Isbn = Hobbit });
        }

        var pushesBefore = _remote.Pushes.Count;
        await _sut.RunAsync();

        Assert.Equal(30, _sut.Filled);
        Assert.Equal(2, _remote.Pushes.Count - pushesBefore); // after 25, and at the end
        Assert.Empty(_store.Outbox);
    }

    [Fact]
    public async Task Stop_ends_the_run()
    {
        for (var i = 0; i < 5; i++)
        {
            await _books.SaveAsync(TestBooks.Make($"h{i}", "The Hobbit") with { Isbn = Hobbit });
        }

        _sut.Changed += () => { if (_sut.Checked == 2) _sut.Stop(); };
        await _sut.RunAsync();

        Assert.Equal(2, _sut.Checked);
        Assert.False(_sut.IsRunning);
    }
}

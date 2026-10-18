using Bookshelf.Features.Books;
using Bookshelf.Features.Lookup;
using Bookshelf.Features.Scan;
using Bookshelf.Models;
using Bookshelf.Services.Sync;
using Bookshelf.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Bookshelf.Tests.Lookup;

public class PendingLookupServiceTests
{
    private const string Hobbit = "9780261102217";
    private const string Emma = "9780141439587";

    private readonly InMemoryLocalStore _store = new();
    private readonly FakeConnectivity _connectivity = new();
    private readonly FakeBookLookup _lookup = new FakeBookLookup().Add(Hobbit, "The Hobbit", "J. R. R. Tolkien", "https://covers/hobbit.jpg");
    private readonly BookService _books;
    private readonly PendingLookupService _sut;

    public PendingLookupServiceTests()
    {
        var time = new FakeTimeProvider(TestBooks.Epoch);
        var sync = new SyncService(_store, new NotConfiguredRemoteStore(), _connectivity, time);
        _books = new BookService(_store, sync, time);
        _sut = new PendingLookupService(_books, _lookup, _connectivity);
    }

    private async Task<Book> PendingAsync(string isbn) =>
        await _books.SaveAsync(ScanService.Placeholder(isbn, BookLookupState.Pending) with { Notes = "Loft box 3" });

    [Fact]
    public async Task Fills_in_found_books_and_keeps_notes()
    {
        var pending = await PendingAsync(Hobbit);

        Assert.Equal(1, await _sut.RunAsync());

        var book = _store.Books[pending.Id];
        Assert.Equal("The Hobbit", book.Title);
        Assert.Equal("J. R. R. Tolkien", book.Author);
        Assert.Equal("https://covers/hobbit.jpg", book.CoverUrl);
        Assert.Equal("Loft box 3", book.Notes);
        Assert.Null(book.LookupState);
    }

    [Fact]
    public async Task Marks_unknown_isbns_as_not_found()
    {
        var pending = await PendingAsync(Emma);

        await _sut.RunAsync();

        Assert.Equal(BookLookupState.NotFound, _store.Books[pending.Id].LookupState);
        Assert.Equal($"ISBN {Emma}", _store.Books[pending.Id].Title);
    }

    [Fact]
    public async Task Leaves_books_pending_when_catalogues_are_unreachable()
    {
        var pending = await PendingAsync(Hobbit);
        _lookup.Unavailable = true;

        Assert.Equal(0, await _sut.RunAsync());

        Assert.Equal(BookLookupState.Pending, _store.Books[pending.Id].LookupState);
    }

    [Fact]
    public async Task Does_nothing_offline_then_runs_on_reconnect()
    {
        _connectivity.Set(false);
        var pending = await PendingAsync(Hobbit);

        Assert.Equal(0, await _sut.RunAsync());
        Assert.Empty(_lookup.Lookups);

        _connectivity.Set(true);
        for (var i = 0; i < 100 && _store.Books[pending.Id].LookupState is not null; i++)
        {
            await Task.Delay(10);
        }

        Assert.Equal("The Hobbit", _store.Books[pending.Id].Title);
    }

    [Fact]
    public async Task Ignores_books_that_are_not_pending()
    {
        await _books.SaveAsync(TestBooks.Make("ok", "Already done") with { Isbn = Hobbit });
        await _books.SaveAsync(ScanService.Placeholder(Emma, BookLookupState.NotFound));

        Assert.Equal(0, await _sut.RunAsync());
        Assert.Empty(_lookup.Lookups);
    }

    [Fact]
    public async Task Fills_in_synopsis_and_genres_too()
    {
        _lookup.Known[Hobbit] = _lookup.Known[Hobbit] with { Synopsis = "There and back again.", Categories = ["Juvenile Fiction / Fantasy & Magic"] };
        var pending = await PendingAsync(Hobbit);

        await _sut.RunAsync();

        Assert.Equal("There and back again.", _store.Books[pending.Id].Synopsis);
        Assert.Equal(["fantasy", "children"], _store.Books[pending.Id].Genres);
    }
}

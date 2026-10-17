using Bookshelf.Features.Books;
using Bookshelf.Features.Scan;
using Bookshelf.Models;
using Bookshelf.Services.Sync;
using Bookshelf.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Bookshelf.Tests.Scan;

public class ScanServiceTests
{
    private const string Hobbit = "9780261102217";
    private const string Emma = "9780141439587";

    private readonly InMemoryLocalStore _store = new();
    private readonly FakeConnectivity _connectivity = new();
    private readonly FakeBookLookup _lookup = new FakeBookLookup().Add(Hobbit, "The Hobbit", "J. R. R. Tolkien", "https://covers/hobbit.jpg");
    private readonly BookService _books;
    private readonly ScanService _sut;

    public ScanServiceTests()
    {
        var time = new FakeTimeProvider(TestBooks.Epoch);
        var sync = new SyncService(_store, new NotConfiguredRemoteStore(), _connectivity, time);
        _books = new BookService(_store, sync, time);
        _sut = new ScanService(_books, _lookup, _connectivity);
    }

    [Theory]
    [InlineData("5012345678900")] // a grocery EAN, not a book
    [InlineData("9780261102218")] // bad checksum
    [InlineData("hello")]
    public async Task Ignores_codes_that_are_not_isbns(string code)
    {
        Assert.Null(await _sut.HandleAsync(code, confirmEach: false));
        Assert.Empty(_store.Books);
    }

    [Fact]
    public async Task Rapid_mode_adds_found_book_straight_away()
    {
        var item = await _sut.HandleAsync(Hobbit, confirmEach: false);

        Assert.Equal(ScanStatus.Added, item!.Status);
        var book = Assert.Single(_store.Books.Values);
        Assert.Equal("The Hobbit", book.Title);
        Assert.Equal("J. R. R. Tolkien", book.Author);
        Assert.Equal("https://covers/hobbit.jpg", book.CoverUrl);
        Assert.Null(book.LookupState);
        Assert.Single(_store.Outbox);
    }

    [Fact]
    public async Task Rapid_mode_accepts_typed_isbn10()
    {
        var item = await _sut.HandleAsync("0-261-10221-4", confirmEach: false);

        Assert.Equal(ScanStatus.Added, item!.Status);
        Assert.Equal(Hobbit, item.Isbn);
    }

    [Fact]
    public async Task Rapid_mode_saves_unknown_isbn_as_not_found_placeholder()
    {
        var item = await _sut.HandleAsync(Emma, confirmEach: false);

        Assert.Equal(ScanStatus.AddedNotFound, item!.Status);
        var book = Assert.Single(_store.Books.Values);
        Assert.Equal($"ISBN {Emma}", book.Title);
        Assert.Equal(Emma, book.Isbn);
        Assert.Equal(BookLookupState.NotFound, book.LookupState);
    }

    [Fact]
    public async Task Offline_scan_saves_pending_placeholder_without_calling_lookup()
    {
        _connectivity.Set(false);

        var item = await _sut.HandleAsync(Hobbit, confirmEach: false);

        Assert.Equal(ScanStatus.AddedPending, item!.Status);
        Assert.Equal(BookLookupState.Pending, Assert.Single(_store.Books.Values).LookupState);
        Assert.Empty(_lookup.Lookups);
    }

    [Fact]
    public async Task Unreachable_catalogues_also_save_as_pending()
    {
        _lookup.Unavailable = true;

        var item = await _sut.HandleAsync(Hobbit, confirmEach: false);

        Assert.Equal(ScanStatus.AddedPending, item!.Status);
    }

    [Fact]
    public async Task Confirm_mode_waits_for_add()
    {
        var item = await _sut.HandleAsync(Hobbit, confirmEach: true);

        Assert.Equal(ScanStatus.Found, item!.Status);
        Assert.True(item.AwaitingDecision);
        Assert.Empty(_store.Books);

        var added = await _sut.AddAsync(item);

        Assert.Equal(ScanStatus.Added, added.Status);
        Assert.Equal("The Hobbit", Assert.Single(_store.Books.Values).Title);
    }

    [Fact]
    public async Task Confirm_mode_offline_add_saves_pending()
    {
        _connectivity.Set(false);
        var item = await _sut.HandleAsync(Hobbit, confirmEach: true);
        Assert.Equal(ScanStatus.NotFound, item!.Status);
        Assert.True(item.Offline);

        var added = await _sut.AddAsync(item);

        Assert.Equal(ScanStatus.AddedPending, added.Status);
    }

    [Fact]
    public void Skip_marks_item_skipped() =>
        Assert.Equal(ScanStatus.Skipped, ScanService.Skip(new ScanItem(Guid.NewGuid(), Hobbit, ScanStatus.Found)).Status);

    [Fact]
    public async Task Duplicate_is_flagged_and_not_added_even_in_rapid_mode()
    {
        await _sut.HandleAsync(Hobbit, confirmEach: false);

        var again = await _sut.HandleAsync("0261102214", confirmEach: false);

        Assert.Equal(ScanStatus.Duplicate, again!.Status);
        Assert.Single(_store.Books);
        Assert.Equal("The Hobbit", again.Book!.Title);
    }

    [Fact]
    public async Task Another_copy_can_be_added_on_purpose()
    {
        await _sut.HandleAsync(Hobbit, confirmEach: false);
        var duplicate = await _sut.HandleAsync(Hobbit, confirmEach: false);

        var copy = await _sut.AddAnotherCopyAsync(duplicate!);

        Assert.Equal(ScanStatus.Added, copy.Status);
        Assert.Equal(2, _store.Books.Count);
        Assert.Equal(2, _store.Books.Values.Select(b => b.Id).Distinct().Count());
    }

    [Fact]
    public async Task Undo_removes_the_added_book()
    {
        var item = await _sut.HandleAsync(Hobbit, confirmEach: false);

        var undone = await _sut.UndoAsync(item!);

        Assert.Equal(ScanStatus.Undone, undone.Status);
        Assert.Empty(await _books.GetAllAsync());
        Assert.True(Assert.Single(_store.Books.Values).IsDeleted);
    }
}

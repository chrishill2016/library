using Bookshelf.Features.Books;
using Bookshelf.Models;
using Bookshelf.Services.Sync;
using Bookshelf.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Bookshelf.Tests.Books;

public class BookServiceTests
{
    private readonly InMemoryLocalStore _store = new();
    private readonly FakeTimeProvider _time = new(TestBooks.Epoch);
    private readonly SyncService _sync;
    private readonly BookService _sut;

    public BookServiceTests()
    {
        _sync = new SyncService(_store, new NotConfiguredRemoteStore(), new FakeConnectivity(), _time);
        _sut = new BookService(_store, _sync, _time);
    }

    [Fact]
    public async Task Save_new_book_stamps_times_trims_and_queues_for_sync()
    {
        var saved = await _sut.SaveAsync(new Book { Id = "b1", Title = "  Dune ", Author = " Frank Herbert ", Isbn = " ", Notes = "" });

        Assert.Equal("Dune", saved.Title);
        Assert.Equal("Frank Herbert", saved.Author);
        Assert.Null(saved.Isbn);
        Assert.Null(saved.Notes);
        Assert.Equal(TestBooks.Epoch, saved.AddedUtc);
        Assert.Equal(TestBooks.Epoch, saved.LastModified);
        Assert.Equal(saved, _store.Books["b1"]);
        Assert.Equal("b1", _store.Outbox["book:b1"].EntityId);
        Assert.Equal(1, _sync.State.PendingCount);
    }

    [Fact]
    public async Task Update_keeps_added_date_and_bumps_last_modified()
    {
        await _sut.SaveAsync(new Book { Id = "b1", Title = "Dune" });
        _time.Advance(TimeSpan.FromHours(2));

        var updated = await _sut.SaveAsync(new Book { Id = "b1", Title = "Dune Messiah" });

        Assert.Equal(TestBooks.Epoch, updated.AddedUtc);
        Assert.Equal(TestBooks.Epoch.AddHours(2), updated.LastModified);
        Assert.Single(_store.Outbox);
    }

    [Fact]
    public async Task Delete_soft_deletes_hides_book_and_queues_for_sync()
    {
        await _sut.SaveAsync(new Book { Id = "b1", Title = "Dune" });
        await _store.RemoveFromOutboxAsync("book:b1");
        _time.Advance(TimeSpan.FromMinutes(1));

        await _sut.DeleteAsync("b1");

        Assert.True(_store.Books["b1"].IsDeleted);
        Assert.Equal(TestBooks.Epoch.AddMinutes(1), _store.Books["b1"].LastModified);
        Assert.Single(_store.Outbox);
        Assert.Empty(await _sut.GetAllAsync());
        Assert.Null(await _sut.GetAsync("b1"));
    }

    [Fact]
    public async Task Delete_of_missing_book_does_nothing()
    {
        await _sut.DeleteAsync("nope");

        Assert.Empty(_store.Books);
        Assert.Empty(_store.Outbox);
    }

    [Fact]
    public async Task Saving_raises_changed_and_refreshes_list()
    {
        var changed = 0;
        _sut.Changed += () => changed++;
        Assert.Empty(await _sut.GetAllAsync());

        await _sut.SaveAsync(new Book { Id = "b1", Title = "Dune" });

        Assert.Equal(1, changed);
        Assert.Single(await _sut.GetAllAsync());
    }

    [Fact]
    public async Task Remote_changes_refresh_the_cached_list()
    {
        var remote = new FakeRemoteStore();
        var sync = new SyncService(_store, remote, new FakeConnectivity(), _time);
        var sut = new BookService(_store, sync, _time);
        Assert.Empty(await sut.GetAllAsync());
        var changed = false;
        sut.Changed += () => changed = true;

        remote.Seed(TestBooks.Make("r1", "From my wife's phone"));
        await sync.SyncNowAsync();

        Assert.True(changed);
        Assert.Equal("From my wife's phone", Assert.Single(await sut.GetAllAsync()).Title);
    }
}

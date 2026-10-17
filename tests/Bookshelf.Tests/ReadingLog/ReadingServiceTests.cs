using Bookshelf.Features.Account;
using Bookshelf.Features.ReadingLog;
using Bookshelf.Models;
using Bookshelf.Services.Firebase;
using Bookshelf.Services.Sync;
using Bookshelf.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Bookshelf.Tests.ReadingLog;

public class ReadingServiceTests : IAsyncLifetime
{
    private static readonly AppUser Chris = new("chris", "chris@example.com", "Chris", null);

    private readonly InMemoryLocalStore _store = new();
    private readonly FakeRemoteStore _remote = new();
    private readonly FakeFirebaseClient _client = new() { RestoredUser = Chris };
    private readonly FakeTimeProvider _time = new(TestBooks.Epoch.AddDays(1));
    private readonly AuthService _auth;
    private readonly ReadingService _sut;

    public ReadingServiceTests()
    {
        var sync = new SyncService(_store, _remote, new FakeConnectivity(), _time);
        _auth = new AuthService(_client);
        _sut = new ReadingService(_store, sync, _auth, _time);
    }

    public Task InitializeAsync() => _auth.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Unrecorded_book_gives_an_empty_unsaved_reading()
    {
        var reading = await _sut.GetMyReadingAsync("b1");

        Assert.Equal("b1_chris", reading.Id);
        Assert.True(reading.IsEmpty);
        Assert.Empty(_store.Readings);
    }

    [Fact]
    public async Task Rating_is_saved_stamped_queued_and_synced()
    {
        var saved = await _sut.SetRatingAsync("b1", 4);

        Assert.Equal(4, _store.Readings["b1_chris"].Rating);
        Assert.Equal("Chris", saved.ReaderName);
        Assert.Equal(_time.GetUtcNow(), saved.LastModified);
        Assert.Equal(4, _remote.Readings["b1_chris"].Rating);
        Assert.Empty(_store.Outbox);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Rejects_ratings_outside_one_to_five(int rating) =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _sut.SetRatingAsync("b1", rating));

    [Fact]
    public async Task Rating_can_be_cleared()
    {
        await _sut.SetRatingAsync("b1", 4);

        await _sut.SetRatingAsync("b1", null);

        Assert.Null(_store.Readings["b1_chris"].Rating);
    }

    [Fact]
    public async Task Changes_keep_the_other_fields()
    {
        await _sut.SetRatingAsync("b1", 5);
        await _sut.SetStatusAsync("b1", ReadingStatus.Reading);
        await _sut.SetReviewAsync("b1", "  Wonderful.  ");

        var reading = _store.Readings["b1_chris"];
        Assert.Equal(5, reading.Rating);
        Assert.Equal(ReadingStatus.Reading, reading.Status);
        Assert.Equal("Wonderful.", reading.Review);
    }

    [Fact]
    public async Task Blank_review_is_cleared_and_overlong_review_rejected()
    {
        await _sut.SetReviewAsync("b1", "Good");
        await _sut.SetReviewAsync("b1", "   ");
        Assert.Null(_store.Readings["b1_chris"].Review);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.SetReviewAsync("b1", new string('x', ReadingService.MaxReviewLength + 1)));
    }

    [Fact]
    public async Task Rejects_unknown_status() =>
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.SetStatusAsync("b1", "abandoned"));

    [Fact]
    public async Task Adding_a_read_marks_it_read_and_keeps_dates_newest_first()
    {
        await _sut.SetStatusAsync("b1", ReadingStatus.Reading);

        await _sut.AddReadAsync("b1", new ReadDate(2015));
        await _sut.AddReadAsync("b1", new ReadDate(2024, 3, 12));
        await _sut.AddReadAsync("b1", new ReadDate(2019, 6));

        var reading = _store.Readings["b1_chris"];
        Assert.Equal(ReadingStatus.Read, reading.Status);
        Assert.Equal([new ReadDate(2024, 3, 12), new ReadDate(2019, 6), new ReadDate(2015)], reading.Reads);
    }

    [Fact]
    public async Task Same_date_twice_is_recorded_once_and_can_be_removed()
    {
        await _sut.AddReadAsync("b1", new ReadDate(2020, 1, 5));
        await _sut.AddReadAsync("b1", new ReadDate(2020, 1, 5));
        Assert.Single(_store.Readings["b1_chris"].Reads);

        await _sut.RemoveReadAsync("b1", new ReadDate(2020, 1, 5));
        Assert.Empty(_store.Readings["b1_chris"].Reads);
    }

    [Fact]
    public async Task Rejects_impossible_dates() =>
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.AddReadAsync("b1", new ReadDate(2023, 2, 29)));

    [Fact]
    public async Task Others_readings_are_listed_but_mine_and_empty_ones_are_not()
    {
        await _store.PutReadingAsync(TestReadings.Make("b1", "wife", rating: 5, name: "Sarah"));
        await _store.PutReadingAsync(TestReadings.Make("b1", "lodger"));
        await _store.PutReadingAsync(TestReadings.Make("b2", "wife", rating: 2, name: "Sarah"));
        await _sut.SetRatingAsync("b1", 3);

        var others = await _sut.GetOthersAsync("b1");

        Assert.Equal(["b1_wife"], others.Select(r => r.Id));
    }

    [Fact]
    public async Task My_readings_are_keyed_by_book()
    {
        await _store.PutReadingAsync(TestReadings.Make("b1", "wife", rating: 5));
        await _sut.SetRatingAsync("b2", 4);

        var mine = await _sut.GetMyReadingsAsync();

        Assert.Equal(["b2"], mine.Keys);
    }

    [Fact]
    public async Task Requires_sign_in()
    {
        await _client.SignOutAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SetRatingAsync("b1", 3));
        Assert.Empty(await _sut.GetMyReadingsAsync());
    }

    [Fact]
    public async Task Raises_changed_and_refreshes_after_sync_pulls()
    {
        var changed = 0;
        _sut.Changed += () => changed++;
        Assert.Empty(await _sut.GetAllAsync());

        await _sut.SetRatingAsync("b1", 4);

        Assert.True(changed >= 1);
        Assert.Single(await _sut.GetAllAsync());
    }
}

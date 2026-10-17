using Bookshelf.Models;
using Bookshelf.Services.Sync;
using Bookshelf.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Bookshelf.Tests.Sync;

public class SyncServiceTests
{
    private readonly InMemoryLocalStore _store = new();
    private readonly FakeRemoteStore _remote = new();
    private readonly FakeConnectivity _connectivity = new();
    private readonly FakeTimeProvider _time = new(TestBooks.Epoch.AddDays(1));

    private SyncService CreateSut() => new(_store, _remote, _connectivity, _time);

    private async Task QueueLocalAsync(Book book) =>
        await _store.PutBookAsync(book, OutboxEntry.ForBook(book.Id, book.LastModified));

    [Fact]
    public async Task Pushes_queued_changes_and_clears_outbox()
    {
        await QueueLocalAsync(TestBooks.Make("a"));
        await QueueLocalAsync(TestBooks.Make("b"));
        var sut = CreateSut();

        var result = await sut.SyncNowAsync();

        Assert.Equal(SyncOutcome.Succeeded, result.Outcome);
        Assert.Equal(2, result.Pushed);
        Assert.Equal(["a", "b"], _remote.Books.Keys.Order());
        Assert.Empty(_store.Outbox);
        Assert.Equal(0, sut.State.PendingCount);
        Assert.Equal(_time.GetUtcNow(), sut.State.LastSyncUtc);
    }

    [Fact]
    public async Task Pulls_remote_books_into_local_store_and_saves_cursor()
    {
        _remote.Seed(TestBooks.Make("r1", "Remote One"));
        _remote.Seed(TestBooks.Make("r2", "Remote Two"));
        var sut = CreateSut();
        var applied = 0;
        sut.RemoteChangesApplied += () => applied++;

        var result = await sut.SyncNowAsync();

        Assert.Equal(2, result.Pulled);
        Assert.Equal("Remote One", _store.Books["r1"].Title);
        Assert.Equal("2", _store.Settings[SyncService.PullCursorKey]);
        Assert.Equal(1, applied);
        Assert.Empty(_store.Outbox);
    }

    [Fact]
    public async Task Next_pull_passes_saved_cursor_and_only_gets_new_changes()
    {
        _remote.Seed(TestBooks.Make("r1"));
        var sut = CreateSut();
        await sut.SyncNowAsync();

        _remote.Seed(TestBooks.Make("r2"));
        var result = await sut.SyncNowAsync();

        Assert.Equal([null, "1"], _remote.CursorsRequested);
        Assert.Equal(1, result.Pulled);
    }

    [Fact]
    public async Task Newer_remote_change_wins_and_drops_queued_local_edit()
    {
        await QueueLocalAsync(TestBooks.Make("x", "Local title", minutes: 5));
        _remote.Seed(TestBooks.Make("x", "Remote title", minutes: 10));
        var sut = CreateSut();

        var result = await sut.SyncNowAsync();

        Assert.Equal("Remote title", _store.Books["x"].Title);
        Assert.Empty(_store.Outbox);
        Assert.Equal(0, result.Pushed);
        Assert.Equal("Remote title", _remote.Books["x"].Title);
    }

    [Fact]
    public async Task Newer_local_change_wins_and_is_pushed()
    {
        await QueueLocalAsync(TestBooks.Make("x", "Local title", minutes: 10));
        _remote.Seed(TestBooks.Make("x", "Remote title", minutes: 5));
        var sut = CreateSut();

        var result = await sut.SyncNowAsync();

        Assert.Equal("Local title", _store.Books["x"].Title);
        Assert.Equal("Local title", _remote.Books["x"].Title);
        Assert.Equal(0, result.Pulled);
        Assert.Equal(1, result.Pushed);
    }

    [Fact]
    public async Task Remote_deletion_is_applied_locally()
    {
        await _store.PutBookAsync(TestBooks.Make("x", minutes: 1));
        _remote.Seed(TestBooks.Make("x", minutes: 2, deleted: true));
        var sut = CreateSut();

        await sut.SyncNowAsync();

        Assert.True(_store.Books["x"].IsDeleted);
    }

    [Fact]
    public async Task Edit_made_during_push_stays_queued()
    {
        await QueueLocalAsync(TestBooks.Make("x", "First", minutes: 1));
        var sut = CreateSut();
        // The pull reads nothing; the first outbox read is the push snapshot.
        _store.AfterGetOutbox = () =>
            _store.Outbox["book:x"] = OutboxEntry.ForBook("x", TestBooks.Epoch.AddMinutes(2));

        await sut.SyncNowAsync();

        Assert.True(_store.Outbox.ContainsKey("book:x"));
        Assert.Equal(1, sut.State.PendingCount);
    }

    [Fact]
    public async Task Does_nothing_when_offline()
    {
        _connectivity.Set(false);
        await QueueLocalAsync(TestBooks.Make("a"));
        var sut = CreateSut();

        var result = await sut.SyncNowAsync();

        Assert.Equal(SyncOutcome.Offline, result.Outcome);
        Assert.Empty(_remote.Pushes);
        Assert.Single(_store.Outbox);
    }

    [Fact]
    public async Task Keeps_changes_queued_when_remote_not_configured()
    {
        _remote.IsConfigured = false;
        await QueueLocalAsync(TestBooks.Make("a"));
        var sut = CreateSut();
        await sut.InitializeAsync();

        var result = await sut.SyncNowAsync();

        Assert.Equal(SyncOutcome.NotConfigured, result.Outcome);
        Assert.Single(_store.Outbox);
        Assert.Equal(1, sut.State.PendingCount);
        Assert.False(sut.State.RemoteConfigured);
    }

    [Fact]
    public async Task Failure_keeps_outbox_records_error_and_notifies()
    {
        await QueueLocalAsync(TestBooks.Make("a"));
        _remote.FailWith = new HttpRequestException("Network unreachable");
        var sut = CreateSut();
        SyncResult? notified = null;
        sut.SyncCompleted += r => notified = r;

        var result = await sut.SyncNowAsync();

        Assert.Equal(SyncOutcome.Failed, result.Outcome);
        Assert.Equal("Network unreachable", sut.State.LastError);
        Assert.Single(_store.Outbox);
        Assert.Equal(SyncOutcome.Failed, notified?.Outcome);
        Assert.False(sut.State.IsSyncing);
    }

    [Fact]
    public async Task Success_clears_previous_error()
    {
        _remote.FailWith = new HttpRequestException("boom");
        var sut = CreateSut();
        await sut.SyncNowAsync();

        _remote.FailWith = null;
        await sut.SyncNowAsync();

        Assert.Null(sut.State.LastError);
    }

    [Fact]
    public async Task Reconnecting_syncs_automatically_and_reports_after_reconnect()
    {
        var sut = CreateSut();
        await sut.InitializeAsync();
        var results = new List<SyncResult>();
        sut.SyncCompleted += results.Add;

        _connectivity.Set(false);
        Assert.False(sut.State.IsOnline);
        await QueueLocalAsync(TestBooks.Make("a"));
        await sut.NotifyLocalChangeAsync();
        Assert.Equal(1, sut.State.PendingCount);

        _connectivity.Set(true);
        await WaitUntilAsync(() => results.Count > 0);

        Assert.True(sut.State.IsOnline);
        var result = Assert.Single(results);
        Assert.True(result.AfterReconnect);
        Assert.Equal(1, result.Pushed);
        Assert.Equal(0, sut.State.PendingCount);
    }

    [Fact]
    public async Task Initialize_loads_pending_count_and_last_sync_then_syncs()
    {
        await QueueLocalAsync(TestBooks.Make("a"));
        _remote.IsConfigured = false;
        _store.Settings[SyncService.LastSyncKey] = TestBooks.Epoch.ToString("O");
        var sut = CreateSut();

        await sut.InitializeAsync();

        Assert.Equal(1, sut.State.PendingCount);
        Assert.Equal(TestBooks.Epoch, sut.State.LastSyncUtc);
    }

    [Fact]
    public async Task Quiet_sync_with_nothing_to_do_does_not_notify()
    {
        var sut = CreateSut();
        var notified = false;
        sut.SyncCompleted += _ => notified = true;

        await sut.SyncNowAsync();

        Assert.False(notified);
    }

    [Fact]
    public async Task Configuring_remote_after_start_syncs_queued_changes()
    {
        _remote.IsConfigured = false;
        await QueueLocalAsync(TestBooks.Make("a"));
        var sut = CreateSut();
        await sut.InitializeAsync();
        Assert.False(sut.State.RemoteConfigured);

        _remote.IsConfigured = true;
        await WaitUntilAsync(() => sut.State.PendingCount == 0);

        Assert.True(sut.State.RemoteConfigured);
        Assert.True(_remote.Books.ContainsKey("a"));
    }

    [Fact]
    public async Task Reset_forgets_cursor_and_last_sync()
    {
        _remote.Seed(TestBooks.Make("r1"));
        var sut = CreateSut();
        await sut.SyncNowAsync();

        await sut.ResetAsync();

        Assert.False(_store.Settings.ContainsKey(SyncService.PullCursorKey));
        Assert.False(_store.Settings.ContainsKey(SyncService.LastSyncKey));
        Assert.Null(sut.State.LastSyncUtc);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "Condition was not met in time.");
    }
}

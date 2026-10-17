using System.Globalization;
using Bookshelf.Models;
using Bookshelf.Services.Browser;
using Bookshelf.Services.Storage;

namespace Bookshelf.Services.Sync;

/// <summary>
/// Pulls remote changes (last-write-wins on <see cref="Book.LastModified"/>),
/// then pushes whatever is left in the local outbox.
/// </summary>
public sealed class SyncService(
    ILocalStore store,
    IRemoteStore remote,
    IConnectivityService connectivity,
    TimeProvider time) : ISyncService, IDisposable
{
    internal const string LastSyncKey = "sync.lastSyncUtc";
    internal const string PullCursorKey = "sync.pullCursor";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;
    private bool _wentOffline;

    public SyncState State { get; private set; } = new(true, false, remote.IsConfigured, 0, null, null);

    public event Action? StateChanged;
    public event Action<SyncResult>? SyncCompleted;
    public event Action? RemoteChangesApplied;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await connectivity.InitializeAsync();
        connectivity.Changed += OnConnectivityChanged;
        remote.ConfigurationChanged += OnRemoteConfigurationChanged;

        SetState(State with
        {
            IsOnline = connectivity.IsOnline,
            RemoteConfigured = remote.IsConfigured,
            PendingCount = await store.GetOutboxCountAsync(),
            LastSyncUtc = ParseDate(await store.GetSettingAsync(LastSyncKey)),
        });

        await SyncNowAsync();
    }

    public async Task NotifyLocalChangeAsync()
    {
        SetState(State with { PendingCount = await store.GetOutboxCountAsync() });
        await SyncNowAsync();
    }

    public async Task ResetAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await store.RemoveSettingAsync(PullCursorKey);
            await store.RemoveSettingAsync(LastSyncKey);
            SetState(State with { LastSyncUtc = null, LastError = null, PendingCount = await store.GetOutboxCountAsync() });
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SyncResult> SyncNowAsync(CancellationToken cancellationToken = default)
    {
        if (!remote.IsConfigured)
        {
            return new SyncResult(SyncOutcome.NotConfigured);
        }

        if (!connectivity.IsOnline)
        {
            return new SyncResult(SyncOutcome.Offline);
        }

        await _gate.WaitAsync(cancellationToken);
        var afterReconnect = _wentOffline;
        SetState(State with { IsSyncing = true });
        SyncResult result;
        try
        {
            var pulled = await PullAsync(cancellationToken);
            var pushed = await PushAsync(cancellationToken);

            var now = time.GetUtcNow();
            await store.SetSettingAsync(LastSyncKey, now.ToString("O", CultureInfo.InvariantCulture));
            _wentOffline = false;

            result = new SyncResult(SyncOutcome.Succeeded, pushed, pulled, afterReconnect);
            SetState(State with { LastSyncUtc = now, LastError = null });

            if (pulled > 0)
            {
                RemoteChangesApplied?.Invoke();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result = new SyncResult(SyncOutcome.Failed, AfterReconnect: afterReconnect, Error: ex.Message);
            SetState(State with { LastError = ex.Message });
        }
        finally
        {
            SetState(State with { IsSyncing = false, PendingCount = await store.GetOutboxCountAsync() });
            _gate.Release();
        }

        if (result.Outcome == SyncOutcome.Failed || result.AfterReconnect || result.Pushed + result.Pulled > 0)
        {
            SyncCompleted?.Invoke(result);
        }

        return result;
    }

    private async Task<int> PullAsync(CancellationToken cancellationToken)
    {
        var cursor = await store.GetSettingAsync(PullCursorKey);
        var changes = await remote.GetChangesAsync(cursor, cancellationToken);
        var applied = 0;

        foreach (var incoming in changes.Books)
        {
            var local = await store.GetBookAsync(incoming.Id);
            if (local is null || incoming.LastModified > local.LastModified)
            {
                await store.PutBookAsync(incoming);
                // The remote copy is newer, so any queued local edit is obsolete.
                await store.RemoveFromOutboxAsync(OutboxEntry.ForBook(incoming.Id, default).Id);
                applied++;
            }
        }

        if (changes.Cursor is not null)
        {
            await store.SetSettingAsync(PullCursorKey, changes.Cursor);
        }

        return applied;
    }

    private async Task<int> PushAsync(CancellationToken cancellationToken)
    {
        var outbox = await store.GetOutboxAsync();
        if (outbox.Count == 0)
        {
            return 0;
        }

        var books = new List<Book>();
        foreach (var entry in outbox.Where(e => e.EntityType == OutboxEntry.BookType))
        {
            if (await store.GetBookAsync(entry.EntityId) is { } book)
            {
                books.Add(book);
            }
        }

        if (books.Count > 0)
        {
            await remote.PutBooksAsync(books, cancellationToken);
        }

        // Only clear entries that were not re-queued by an edit made while pushing.
        var current = (await store.GetOutboxAsync()).ToDictionary(e => e.Id);
        foreach (var entry in outbox)
        {
            if (current.TryGetValue(entry.Id, out var now) && now.QueuedAt == entry.QueuedAt)
            {
                await store.RemoveFromOutboxAsync(entry.Id);
            }
        }

        return books.Count;
    }

    private async void OnConnectivityChanged(bool isOnline)
    {
        SetState(State with { IsOnline = isOnline });
        if (!isOnline)
        {
            _wentOffline = true;
            return;
        }

        try
        {
            await SyncNowAsync();
        }
        catch (Exception)
        {
            // SyncNowAsync records failures in State; never let an event handler crash the app.
        }
    }

    private async void OnRemoteConfigurationChanged()
    {
        SetState(State with { RemoteConfigured = remote.IsConfigured, LastError = null });
        try
        {
            await SyncNowAsync();
        }
        catch (Exception)
        {
            // Failures are recorded in State.
        }
    }

    private void SetState(SyncState state)
    {
        if (state == State)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke();
    }

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date) ? date : null;

    public void Dispose()
    {
        connectivity.Changed -= OnConnectivityChanged;
        remote.ConfigurationChanged -= OnRemoteConfigurationChanged;
        _gate.Dispose();
    }
}

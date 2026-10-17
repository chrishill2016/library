using Bookshelf.Models;

namespace Bookshelf.Services.Sync;

public interface ISyncService
{
    SyncState State { get; }

    /// <summary>Raised whenever <see cref="State"/> changes.</summary>
    event Action? StateChanged;

    /// <summary>Raised after a sync attempt that pushed or pulled data, failed, or ran after reconnecting.</summary>
    event Action<SyncResult>? SyncCompleted;

    /// <summary>Raised after remote changes were written to the local store.</summary>
    event Action? RemoteChangesApplied;

    /// <summary>Loads state, watches connectivity and runs a first sync.</summary>
    Task InitializeAsync();

    Task<SyncResult> SyncNowAsync(CancellationToken cancellationToken = default);

    /// <summary>Call after queuing a local change; refreshes the pending count and syncs if possible.</summary>
    Task NotifyLocalChangeAsync();
}

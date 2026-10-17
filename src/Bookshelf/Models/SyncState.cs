namespace Bookshelf.Models;

public sealed record SyncState(
    bool IsOnline,
    bool IsSyncing,
    bool RemoteConfigured,
    int PendingCount,
    DateTimeOffset? LastSyncUtc,
    string? LastError);

public enum SyncOutcome
{
    Succeeded,
    Failed,
    Offline,
    NotConfigured,
}

public sealed record SyncResult(SyncOutcome Outcome, int Pushed = 0, int Pulled = 0, bool AfterReconnect = false, string? Error = null);

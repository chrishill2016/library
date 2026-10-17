using System.Globalization;
using Bookshelf.Models;
using Bookshelf.Services.Sync;

namespace Bookshelf.Services.Firebase;

/// <summary>
/// Syncs books with <c>libraries/{id}/books</c>. The pull cursor is the newest
/// server-side <c>syncedAt</c> seen, so device clocks never matter.
/// </summary>
public sealed class FirestoreRemoteStore : IRemoteStore, IDisposable
{
    /// <summary>
    /// Writes commit with server timestamps that can land slightly out of order,
    /// so each pull re-reads this window. Re-reading is harmless (last write wins).
    /// </summary>
    internal const long OverlapMillis = 2 * 60 * 1000;

    private readonly IFirebaseClient _client;
    private readonly RemoteSession _session;

    public FirestoreRemoteStore(IFirebaseClient client, RemoteSession session)
    {
        _client = client;
        _session = session;
        _session.Changed += OnSessionChanged;
    }

    public bool IsConfigured => _session.LibraryId is not null;

    public event Action? ConfigurationChanged;

    public async Task<RemoteChangeSet> GetChangesAsync(string? cursor, CancellationToken cancellationToken = default)
    {
        var libraryId = RequireLibrary();
        long? since = long.TryParse(cursor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
        long? from = since is null ? null : Math.Max(0, since.Value - OverlapMillis);

        var changes = await _client.GetBookChangesAsync(libraryId, from);

        var newest = changes.Count == 0 ? since : Math.Max(since ?? 0, changes.Max(c => c.SyncedAt));
        return new RemoteChangeSet(
            [.. changes.Select(c => c.Book)],
            newest?.ToString(CultureInfo.InvariantCulture));
    }

    public Task PutBooksAsync(IReadOnlyList<Book> books, CancellationToken cancellationToken = default) =>
        _client.PutBooksAsync(RequireLibrary(), books);

    private string RequireLibrary() =>
        _session.LibraryId ?? throw new InvalidOperationException("Not signed in to a library.");

    private void OnSessionChanged() => ConfigurationChanged?.Invoke();

    public void Dispose() => _session.Changed -= OnSessionChanged;
}

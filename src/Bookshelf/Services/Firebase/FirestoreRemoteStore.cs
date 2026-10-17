using System.Globalization;
using Bookshelf.Models;
using Bookshelf.Services.Sync;

namespace Bookshelf.Services.Firebase;

/// <summary>
/// Syncs books with <c>libraries/{id}/books</c> and readings with <c>libraries/{id}/readings</c>.
/// The pull cursor is the newest server-side <c>syncedAt</c> seen in each, as "books;readings",
/// so device clocks never matter. A cursor without ";" (from before readings) is books only.
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
        var (booksSince, readingsSince) = ParseCursor(cursor);

        var books = await _client.GetBookChangesAsync(libraryId, From(booksSince));
        var readings = await _client.GetReadingChangesAsync(libraryId, From(readingsSince));

        var newestBooks = Newest(booksSince, books.Select(b => b.SyncedAt));
        var newestReadings = Newest(readingsSince, readings.Select(r => r.SyncedAt));
        return new RemoteChangeSet(
            [.. books.Select(c => c.Book)],
            [.. readings.Select(c => c.Reading)],
            FormatCursor(newestBooks, newestReadings));
    }

    public Task PutBooksAsync(IReadOnlyList<Book> books, CancellationToken cancellationToken = default) =>
        _client.PutBooksAsync(RequireLibrary(), books);

    public Task PutReadingsAsync(IReadOnlyList<Reading> readings, CancellationToken cancellationToken = default) =>
        _client.PutReadingsAsync(RequireLibrary(), readings);

    internal static (long? Books, long? Readings) ParseCursor(string? cursor)
    {
        if (cursor is null)
        {
            return (null, null);
        }

        var parts = cursor.Split(';');
        return (ParseMillis(parts[0]), parts.Length > 1 ? ParseMillis(parts[1]) : null);
    }

    internal static string? FormatCursor(long? books, long? readings) =>
        books is null && readings is null
            ? null
            : $"{books?.ToString(CultureInfo.InvariantCulture)};{readings?.ToString(CultureInfo.InvariantCulture)}";

    private static long? ParseMillis(string text) =>
        long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static long? From(long? since) => since is null ? null : Math.Max(0, since.Value - OverlapMillis);

    /// <summary>The cursor never moves backwards (only older docs inside the overlap may come back).</summary>
    private static long? Newest(long? since, IEnumerable<long> seen)
    {
        var list = seen.ToList();
        return list.Count == 0 ? since : Math.Max(since ?? 0, list.Max());
    }

    private string RequireLibrary() =>
        _session.LibraryId ?? throw new InvalidOperationException("Not signed in to a library.");

    private void OnSessionChanged() => ConfigurationChanged?.Invoke();

    public void Dispose() => _session.Changed -= OnSessionChanged;
}

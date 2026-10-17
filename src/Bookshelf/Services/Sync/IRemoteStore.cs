using Bookshelf.Models;

namespace Bookshelf.Services.Sync;

/// <summary>The cloud database the local store syncs with.</summary>
public interface IRemoteStore
{
    /// <summary>False until a remote database (and sign-in) is set up.</summary>
    bool IsConfigured { get; }

    /// <summary>Raised when <see cref="IsConfigured"/> changes (e.g. sign-in, sign-out, library chosen).</summary>
    event Action? ConfigurationChanged;

    /// <summary>
    /// Books changed since <paramref name="cursor"/> (everything when null), including deletions.
    /// The cursor is opaque so the remote can use its own server-side clock.
    /// </summary>
    Task<RemoteChangeSet> GetChangesAsync(string? cursor, CancellationToken cancellationToken = default);

    Task PutBooksAsync(IReadOnlyList<Book> books, CancellationToken cancellationToken = default);
}

/// <param name="Books">Changed books.</param>
/// <param name="Cursor">Pass back on the next pull; null keeps the previous cursor.</param>
public sealed record RemoteChangeSet(IReadOnlyList<Book> Books, string? Cursor);

/// <summary>
/// Placeholder used until Firebase is wired up. Changes stay queued
/// in the local outbox and upload once a real remote is registered.
/// </summary>
public sealed class NotConfiguredRemoteStore : IRemoteStore
{
    public bool IsConfigured => false;

    public event Action? ConfigurationChanged
    {
        add { }
        remove { }
    }

    public Task<RemoteChangeSet> GetChangesAsync(string? cursor, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("No remote database is configured.");

    public Task PutBooksAsync(IReadOnlyList<Book> books, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("No remote database is configured.");
}

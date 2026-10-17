namespace Bookshelf.Models;

/// <summary>A book in the shared library.</summary>
public sealed record Book
{
    public required string Id { get; init; }
    public string Title { get; init; } = "";
    public string Author { get; init; } = "";
    public string? Isbn { get; init; }
    public string? Publisher { get; init; }
    public int? PublishedYear { get; init; }
    public string? CoverUrl { get; init; }

    /// <summary>Our own cover photo (Firebase Storage <c>libraries/{id}/covers/{PhotoId}.jpg</c>); shown instead of <see cref="CoverUrl"/>.</summary>
    public string? PhotoId { get; init; }
    public string? Notes { get; init; }
    public DateTimeOffset AddedUtc { get; init; }

    /// <summary>UTC time of the last change; used for last-write-wins sync.</summary>
    public DateTimeOffset LastModified { get; init; }

    /// <summary>Soft-delete marker so deletions can sync to other devices.</summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Set when a scanned book was saved by ISBN only (offline or not found yet).
    /// See <see cref="BookLookupState"/>. Null once details are filled in.
    /// </summary>
    public string? LookupState { get; init; }

    /// <summary>
    /// Wanted but not owned yet (to buy or borrow): kept off the shelf and shown on reading lists.
    /// False for older books, which are all owned.
    /// </summary>
    public bool OnWishlist { get; init; }

    public static string NewId() => Guid.NewGuid().ToString("N");
}

/// <summary>Values for <see cref="Book.LookupState"/> (strings so older clients ignore them).</summary>
public static class BookLookupState
{
    /// <summary>Details will be looked up automatically when online.</summary>
    public const string Pending = "pending";

    /// <summary>Looked up but not found online; needs adding by hand.</summary>
    public const string NotFound = "notFound";
}

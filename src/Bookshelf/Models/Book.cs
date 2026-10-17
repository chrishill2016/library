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
    public string? Notes { get; init; }
    public DateTimeOffset AddedUtc { get; init; }

    /// <summary>UTC time of the last change; used for last-write-wins sync.</summary>
    public DateTimeOffset LastModified { get; init; }

    /// <summary>Soft-delete marker so deletions can sync to other devices.</summary>
    public bool IsDeleted { get; init; }

    public static string NewId() => Guid.NewGuid().ToString("N");
}

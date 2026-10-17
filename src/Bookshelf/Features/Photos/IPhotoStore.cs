using Microsoft.AspNetCore.Components;

namespace Bookshelf.Features.Photos;

/// <summary>A photo just taken: stored on this device but not yet attached to a book.</summary>
public sealed record CapturedPhoto(string Id, string Url, long Bytes);

public sealed record PhotoQueueResult(int Uploaded, int Deleted);

/// <summary>Cover photo storage on the device (IndexedDB) and in Firebase Storage. See wwwroot/js/photos.js.</summary>
public interface IPhotoStore
{
    /// <summary>Resizes and stores the photo chosen in a file input; null if nothing was chosen.</summary>
    Task<CapturedPhoto?> CaptureAsync(ElementReference fileInput);

    /// <summary>A displayable URL: from this device, else downloaded from the library (when given and online).</summary>
    Task<string?> GetUrlAsync(string photoId, string? libraryId);

    /// <summary>The photo now belongs to a saved book, so it should be uploaded.</summary>
    Task MarkAttachedAsync(string photoId);

    /// <summary>The photo is no longer used: remove it here and (when uploaded) from Storage.</summary>
    Task MarkForDeletionAsync(string photoId);

    /// <summary>Uploads and deletes queued photos for the library. Throws when offline or denied.</summary>
    Task<PhotoQueueResult> ProcessQueueAsync(string libraryId);

    Task<int> PendingCountAsync();
}

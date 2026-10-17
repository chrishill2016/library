using Microsoft.AspNetCore.Components;

namespace Bookshelf.Features.Photos;

/// <summary>A photo just taken: stored on this device but not yet attached to a book.</summary>
public sealed record CapturedPhoto(string Id, string Url, long Bytes);

/// <summary>A photo just chosen, held in memory until it's cropped. Found is false when no outline could be guessed.</summary>
public sealed record PhotoDraft(string Id, bool Found);

public sealed record PhotoQueueResult(int Uploaded, int Deleted);

/// <summary>Cover photo storage on the device (IndexedDB) and in Firebase Storage. See wwwroot/js/photos.js.</summary>
public interface IPhotoStore
{
    /// <summary>Opens the photo chosen in a file input for cropping, with a guessed outline; null if nothing was chosen.</summary>
    Task<PhotoDraft?> OpenDraftAsync(ElementReference fileInput);

    /// <summary>Shows the draft with draggable corner handles inside the given element.</summary>
    Task ShowCropperAsync(ElementReference stage, string draftId);

    /// <summary>Moves the corners back to the guess, or out to the whole photo.</summary>
    Task ResetCornersAsync(string draftId, bool wholePhoto);

    /// <summary>Straightens the outlined cover, shrinks it and stores it on this device.</summary>
    Task<CapturedPhoto> SaveDraftAsync(string draftId);

    Task DiscardDraftAsync(string draftId);

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

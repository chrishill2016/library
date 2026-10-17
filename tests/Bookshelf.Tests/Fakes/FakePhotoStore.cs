using Bookshelf.Features.Photos;
using Microsoft.AspNetCore.Components;

namespace Bookshelf.Tests.Fakes;

/// <summary>In-memory stand-in for photos.js: tracks local photos and what's in "Storage".</summary>
public sealed class FakePhotoStore : IPhotoStore
{
    public sealed record Photo(bool Attached, bool Uploaded, bool PendingDelete);

    private int _next;

    public Dictionary<string, Photo> Local { get; } = [];
    public HashSet<string> Remote { get; } = [];
    public bool Offline { get; set; }
    public List<string> Downloads { get; } = [];

    /// <summary>The next photo "chosen" in the file input (null = picker cancelled).</summary>
    public bool NextCaptureCancelled { get; set; }

    /// <summary>Whether the next draft gets a guessed outline.</summary>
    public bool NextDraftFound { get; set; } = true;

    /// <summary>Drafts open for cropping, with the last reset applied (null = guess, true = whole photo).</summary>
    public Dictionary<string, bool?> Drafts { get; } = [];

    public List<string> CroppersShown { get; } = [];

    public Task<PhotoDraft?> OpenDraftAsync(ElementReference fileInput)
    {
        if (NextCaptureCancelled)
        {
            return Task.FromResult<PhotoDraft?>(null);
        }

        var id = $"draft{++_next:D4}";
        Drafts[id] = null;
        return Task.FromResult<PhotoDraft?>(new PhotoDraft(id, NextDraftFound));
    }

    public Task ShowCropperAsync(ElementReference stage, string draftId)
    {
        CroppersShown.Add(draftId);
        return Task.CompletedTask;
    }

    public Task ResetCornersAsync(string draftId, bool wholePhoto)
    {
        Drafts[draftId] = wholePhoto;
        return Task.CompletedTask;
    }

    public Task<CapturedPhoto> SaveDraftAsync(string draftId)
    {
        if (!Drafts.Remove(draftId))
        {
            throw new InvalidOperationException("No such draft.");
        }

        var id = $"photo{++_next:D4}abcdef";
        Local[id] = new Photo(false, false, false);
        return Task.FromResult(new CapturedPhoto(id, $"blob:{id}", 120_000));
    }

    public Task DiscardDraftAsync(string draftId)
    {
        Drafts.Remove(draftId);
        return Task.CompletedTask;
    }

    public Task<string?> GetUrlAsync(string photoId, string? libraryId)
    {
        if (Local.TryGetValue(photoId, out var photo) && !photo.PendingDelete)
        {
            return Task.FromResult<string?>($"blob:{photoId}");
        }

        if (libraryId is not null && !Offline && Remote.Contains(photoId))
        {
            Downloads.Add(photoId);
            Local[photoId] = new Photo(true, true, false);
            return Task.FromResult<string?>($"blob:{photoId}");
        }

        return Task.FromResult<string?>(null);
    }

    public Task MarkAttachedAsync(string photoId)
    {
        if (Local.TryGetValue(photoId, out var photo))
        {
            Local[photoId] = photo with { Attached = true };
        }

        return Task.CompletedTask;
    }

    public Task MarkForDeletionAsync(string photoId)
    {
        if (Local.TryGetValue(photoId, out var photo) && !photo.Attached && !photo.Uploaded)
        {
            Local.Remove(photoId);
        }
        else
        {
            Local[photoId] = new Photo(true, true, true);
        }

        return Task.CompletedTask;
    }

    public Task<PhotoQueueResult> ProcessQueueAsync(string libraryId)
    {
        if (Offline)
        {
            throw new HttpRequestException("offline");
        }

        int uploaded = 0, deleted = 0;
        foreach (var (id, photo) in Local.ToList())
        {
            if (photo.PendingDelete)
            {
                Remote.Remove(id);
                Local.Remove(id);
                deleted++;
            }
            else if (photo.Attached && !photo.Uploaded)
            {
                Remote.Add(id);
                Local[id] = photo with { Uploaded = true };
                uploaded++;
            }
        }

        return Task.FromResult(new PhotoQueueResult(uploaded, deleted));
    }

    public Task<int> PendingCountAsync() =>
        Task.FromResult(Local.Values.Count(p => p.PendingDelete || (p.Attached && !p.Uploaded)));
}

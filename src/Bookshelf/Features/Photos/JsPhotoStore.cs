using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Bookshelf.Features.Photos;

public sealed class JsPhotoStore(IJSRuntime js) : IPhotoStore, IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _module =
        new(() => js.InvokeAsync<IJSObjectReference>("import", "./js/photos.js").AsTask());

    public async Task<PhotoDraft?> OpenDraftAsync(ElementReference fileInput) =>
        await (await _module.Value).InvokeAsync<PhotoDraft?>("openDraft", fileInput);

    public async Task ShowCropperAsync(ElementReference stage, string draftId) =>
        await (await _module.Value).InvokeVoidAsync("showCropper", stage, draftId);

    public async Task ResetCornersAsync(string draftId, bool wholePhoto) =>
        await (await _module.Value).InvokeVoidAsync("resetCorners", draftId, wholePhoto);

    public async Task<CapturedPhoto> SaveDraftAsync(string draftId) =>
        await (await _module.Value).InvokeAsync<CapturedPhoto>("saveDraft", draftId);

    public async Task DiscardDraftAsync(string draftId) =>
        await (await _module.Value).InvokeVoidAsync("discardDraft", draftId);

    public async Task<string?> GetUrlAsync(string photoId, string? libraryId) =>
        await (await _module.Value).InvokeAsync<string?>("getUrl", photoId, libraryId);

    public async Task MarkAttachedAsync(string photoId) =>
        await (await _module.Value).InvokeVoidAsync("markAttached", photoId);

    public async Task MarkForDeletionAsync(string photoId) =>
        await (await _module.Value).InvokeVoidAsync("markForDeletion", photoId);

    public async Task<PhotoQueueResult> ProcessQueueAsync(string libraryId) =>
        await (await _module.Value).InvokeAsync<PhotoQueueResult>("processQueue", libraryId);

    public async Task<int> PendingCountAsync() =>
        await (await _module.Value).InvokeAsync<int>("pendingCount");

    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated)
        {
            try
            {
                await (await _module.Value).DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}

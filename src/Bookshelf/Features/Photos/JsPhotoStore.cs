using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Bookshelf.Features.Photos;

public sealed class JsPhotoStore(IJSRuntime js) : IPhotoStore, IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _module =
        new(() => js.InvokeAsync<IJSObjectReference>("import", "./js/photos.js").AsTask());

    public async Task<CapturedPhoto?> CaptureAsync(ElementReference fileInput) =>
        await (await _module.Value).InvokeAsync<CapturedPhoto?>("captureFromInput", fileInput);

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

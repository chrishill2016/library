using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Bookshelf.Features.Scan;

/// <summary>Wraps wwwroot/js/scanner.js (camera + BarcodeDetector).</summary>
public interface IBarcodeScanner
{
    Task<bool> IsSupportedAsync();

    /// <summary>Starts the camera in <paramref name="video"/>; returns whether a torch is available.</summary>
    Task<bool> StartAsync(ElementReference video, Func<string, Task> onBarcode);

    Task SetTorchAsync(bool on);

    Task StopAsync();
}

public sealed class BarcodeScanner(IJSRuntime js) : IBarcodeScanner, IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _module =
        new(() => js.InvokeAsync<IJSObjectReference>("import", "./js/scanner.js").AsTask());

    private DotNetObjectReference<Callback>? _callback;

    public async Task<bool> IsSupportedAsync() => await (await _module.Value).InvokeAsync<bool>("isSupported");

    public async Task<bool> StartAsync(ElementReference video, Func<string, Task> onBarcode)
    {
        _callback?.Dispose();
        _callback = DotNetObjectReference.Create(new Callback(onBarcode));
        var result = await (await _module.Value).InvokeAsync<StartResult>("start", video, _callback);
        return result.HasTorch;
    }

    public async Task SetTorchAsync(bool on) => await (await _module.Value).InvokeVoidAsync("setTorch", on);

    public async Task StopAsync()
    {
        if (_module.IsValueCreated)
        {
            await (await _module.Value).InvokeVoidAsync("stop");
        }

        _callback?.Dispose();
        _callback = null;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
            if (_module.IsValueCreated)
            {
                await (await _module.Value).DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private sealed record StartResult(bool HasTorch);

    public sealed class Callback(Func<string, Task> onBarcode)
    {
        [JSInvokable]
        public Task OnBarcode(string value) => onBarcode(value);
    }
}

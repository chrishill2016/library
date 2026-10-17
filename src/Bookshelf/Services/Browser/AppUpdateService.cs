using Microsoft.JSInterop;

namespace Bookshelf.Services.Browser;

/// <summary>Tells the UI when a new service worker version is waiting.</summary>
public sealed class AppUpdateService(IJSRuntime js) : IDisposable
{
    private DotNetObjectReference<AppUpdateService>? _ref;
    private IJSObjectReference? _module;

    public bool UpdateAvailable { get; private set; }

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (_ref is not null)
        {
            return;
        }

        _ref = DotNetObjectReference.Create(this);
        _module = await js.InvokeAsync<IJSObjectReference>("import", "./js/browser.js");
        await _module.InvokeVoidAsync("watchForUpdates", _ref);
    }

    [JSInvokable]
    public void OnUpdateAvailable()
    {
        UpdateAvailable = true;
        Changed?.Invoke();
    }

    public async Task ApplyUpdateAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("applyUpdate");
        }
    }

    public void Dispose() => _ref?.Dispose();
}

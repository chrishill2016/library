using Microsoft.JSInterop;

namespace Bookshelf.Services.Browser;

public sealed class BrowserConnectivityService(IJSRuntime js) : IConnectivityService, IDisposable
{
    private DotNetObjectReference<BrowserConnectivityService>? _ref;

    public bool IsOnline { get; private set; } = true;

    public event Action<bool>? Changed;

    public async Task InitializeAsync()
    {
        if (_ref is not null)
        {
            return;
        }

        _ref = DotNetObjectReference.Create(this);
        var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/browser.js");
        IsOnline = await module.InvokeAsync<bool>("isOnline");
        await module.InvokeVoidAsync("watchConnectivity", _ref);
    }

    [JSInvokable]
    public void OnConnectivityChanged(bool isOnline)
    {
        if (IsOnline == isOnline)
        {
            return;
        }

        IsOnline = isOnline;
        Changed?.Invoke(isOnline);
    }

    public void Dispose() => _ref?.Dispose();
}

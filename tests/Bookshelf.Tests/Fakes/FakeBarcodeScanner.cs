using Bookshelf.Features.Scan;
using Microsoft.AspNetCore.Components;

namespace Bookshelf.Tests.Fakes;

public sealed class FakeBarcodeScanner : IBarcodeScanner
{
    private Func<string, Task>? _onBarcode;

    public bool Supported { get; set; }
    public bool Started { get; private set; }

    public Task<bool> IsSupportedAsync() => Task.FromResult(Supported);

    public Task<bool> StartAsync(ElementReference video, Func<string, Task> onBarcode)
    {
        _onBarcode = onBarcode;
        Started = true;
        return Task.FromResult(false);
    }

    /// <summary>Simulates the camera seeing a barcode.</summary>
    public Task ScanAsync(string code) => _onBarcode?.Invoke(code) ?? Task.CompletedTask;

    public Task SetTorchAsync(bool on) => Task.CompletedTask;

    public Task StopAsync()
    {
        Started = false;
        return Task.CompletedTask;
    }
}

using Bookshelf.Services.Browser;

namespace Bookshelf.Tests.Fakes;

public sealed class FakeConnectivity(bool isOnline = true) : IConnectivityService
{
    public bool IsOnline { get; private set; } = isOnline;

    public event Action<bool>? Changed;

    public Task InitializeAsync() => Task.CompletedTask;

    public void Set(bool isOnline)
    {
        IsOnline = isOnline;
        Changed?.Invoke(isOnline);
    }
}

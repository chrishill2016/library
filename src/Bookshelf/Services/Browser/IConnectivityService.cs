namespace Bookshelf.Services.Browser;

public interface IConnectivityService
{
    bool IsOnline { get; }

    event Action<bool>? Changed;

    Task InitializeAsync();
}

namespace Bookshelf.Services.Firebase;

/// <summary>Which library the remote store syncs with; null when signed out or not set up.</summary>
public sealed class RemoteSession
{
    public string? LibraryId { get; private set; }

    public event Action? Changed;

    public void SetLibrary(string? libraryId)
    {
        if (LibraryId == libraryId)
        {
            return;
        }

        LibraryId = libraryId;
        Changed?.Invoke();
    }
}

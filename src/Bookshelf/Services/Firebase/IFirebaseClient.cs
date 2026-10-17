using Bookshelf.Models;

namespace Bookshelf.Services.Firebase;

/// <summary>Firebase Auth and Firestore calls the app needs.</summary>
public interface IFirebaseClient
{
    /// <summary>Raised with the current user (or null) on startup and whenever sign-in state changes.</summary>
    event Action<AppUser?>? AuthStateChanged;

    /// <summary>Starts Firebase and waits until the saved sign-in (if any) has been restored.</summary>
    Task InitializeAsync();

    Task SignInWithGoogleAsync();

    Task SignOutAsync();

    Task<IReadOnlyList<LibraryInfo>> GetMyLibrariesAsync(string uid);

    Task<IReadOnlyList<LibraryInfo>> GetInvitesAsync(string email);

    Task<LibraryInfo?> GetLibraryAsync(string libraryId);

    Task<LibraryInfo> CreateLibraryAsync(string name, AppUser owner);

    Task AcceptInviteAsync(string libraryId, AppUser user);

    Task InviteAsync(string libraryId, string email);

    Task CancelInviteAsync(string libraryId, string email);

    Task<IReadOnlyList<RemoteBook>> GetBookChangesAsync(string libraryId, long? sinceMillis);

    Task PutBooksAsync(string libraryId, IReadOnlyList<Book> books);
}

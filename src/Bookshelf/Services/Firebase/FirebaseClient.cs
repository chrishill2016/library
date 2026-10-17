using Bookshelf.Models;
using Microsoft.JSInterop;

namespace Bookshelf.Services.Firebase;

public sealed class FirebaseClient(IJSRuntime js, FirebaseOptions options) : IFirebaseClient, IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _module =
        new(() => js.InvokeAsync<IJSObjectReference>("import", "./js/firebase.js").AsTask());

    private DotNetObjectReference<FirebaseClient>? _ref;

    public event Action<AppUser?>? AuthStateChanged;

    public async Task InitializeAsync()
    {
        _ref ??= DotNetObjectReference.Create(this);
        await Call("initialize", options, _ref);
    }

    [JSInvokable]
    public void OnAuthStateChanged(AppUser? user) => AuthStateChanged?.Invoke(user);

    public Task SignInWithGoogleAsync() => Call("signInWithGoogle");

    public Task SignOutAsync() => Call("signOut");

    public async Task<IReadOnlyList<LibraryInfo>> GetMyLibrariesAsync(string uid) =>
        await Call<LibraryInfo[]>("getMyLibraries", uid);

    public async Task<IReadOnlyList<LibraryInfo>> GetInvitesAsync(string email) =>
        await Call<LibraryInfo[]>("getInvites", email);

    public Task<LibraryInfo?> GetLibraryAsync(string libraryId) => Call<LibraryInfo?>("getLibrary", libraryId);

    public Task<LibraryInfo> CreateLibraryAsync(string name, AppUser owner) => Call<LibraryInfo>("createLibrary", name, owner);

    public Task AcceptInviteAsync(string libraryId, AppUser user) => Call("acceptInvite", libraryId, user);

    public Task InviteAsync(string libraryId, string email) => Call("invite", libraryId, email);

    public Task CancelInviteAsync(string libraryId, string email) => Call("cancelInvite", libraryId, email);

    public async Task<IReadOnlyList<RemoteBook>> GetBookChangesAsync(string libraryId, long? sinceMillis) =>
        await Call<RemoteBook[]>("getBookChanges", libraryId, sinceMillis);

    public Task PutBooksAsync(string libraryId, IReadOnlyList<Book> books) => Call("putBooks", libraryId, books);

    public async Task<IReadOnlyList<RemoteReading>> GetReadingChangesAsync(string libraryId, long? sinceMillis) =>
        await Call<RemoteReading[]>("getReadingChanges", libraryId, sinceMillis);

    public Task PutReadingsAsync(string libraryId, IReadOnlyList<Reading> readings) => Call("putReadings", libraryId, readings);

    private async Task Call(string identifier, params object?[] args) =>
        await (await _module.Value).InvokeVoidAsync(identifier, args);

    private async Task<T> Call<T>(string identifier, params object?[] args) =>
        await (await _module.Value).InvokeAsync<T>(identifier, args);

    public async ValueTask DisposeAsync()
    {
        _ref?.Dispose();
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

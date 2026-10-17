using Bookshelf.Features.Account;
using Bookshelf.Features.Books;
using Bookshelf.Services.Browser;
using Bookshelf.Services.Firebase;
using Bookshelf.Services.Storage;
using Bookshelf.Services.Sync;

namespace Bookshelf.Features.Library;

public enum LibraryStatus
{
    /// <summary>Waiting for sign-in to be restored or the library to load.</summary>
    Loading,
    SignedOut,
    /// <summary>Signed in but not a member of a library yet: create one or accept an invite.</summary>
    NeedsSetup,
    /// <summary>Signed in, no library known on this device, and offline.</summary>
    OfflineSetup,
    Error,
    Ready,
}

/// <summary>
/// Works out which shared library the signed-in user belongs to, and handles
/// creating it, inviting and joining. Points sync at that library when ready.
/// </summary>
public sealed class LibraryService : IDisposable
{
    public const string DefaultName = "The Hill Library";

    internal const string AccountUidKey = "account.uid";
    internal const string LibraryIdKey = "library.id";
    internal const string LibraryNameKey = "library.name";

    private readonly AuthService _auth;
    private readonly IFirebaseClient _client;
    private readonly ILocalStore _store;
    private readonly ISyncService _sync;
    private readonly BookService _books;
    private readonly RemoteSession _session;
    private readonly IConnectivityService _connectivity;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LibraryService(
        AuthService auth,
        IFirebaseClient client,
        ILocalStore store,
        ISyncService sync,
        BookService books,
        RemoteSession session,
        IConnectivityService connectivity)
    {
        _auth = auth;
        _client = client;
        _store = store;
        _sync = sync;
        _books = books;
        _session = session;
        _connectivity = connectivity;
        _auth.Changed += OnAuthChanged;
        _connectivity.Changed += OnConnectivityChanged;
    }

    public LibraryStatus Status { get; private set; } = LibraryStatus.Loading;

    public LibraryInfo? Current { get; private set; }

    /// <summary>Libraries the signed-in user has been invited to (shown during setup).</summary>
    public IReadOnlyList<LibraryInfo> Invites { get; private set; } = [];

    public string? Error { get; private set; }

    public bool IsOwner => Current is not null && Current.OwnerUid == _auth.User?.Uid;

    public event Action? Changed;

    /// <summary>Re-evaluates the library for the current user.</summary>
    public async Task RefreshAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await LoadAsync();
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke();
    }

    public async Task CreateAsync(string name)
    {
        var user = RequireUser();
        var library = await _client.CreateLibraryAsync(string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim(), user);
        await UseLibraryAsync(library);
        Changed?.Invoke();
    }

    public async Task AcceptInviteAsync(LibraryInfo invite)
    {
        var user = RequireUser();
        await _client.AcceptInviteAsync(invite.Id, user);
        await UseLibraryAsync(await _client.GetLibraryAsync(invite.Id) ?? invite);
        Changed?.Invoke();
    }

    /// <summary>Invites someone by their Google email. Returns an error message, or null on success.</summary>
    public async Task<string?> InviteAsync(string email)
    {
        if (Current is null || !IsOwner)
        {
            return "Only the library's owner can invite people.";
        }

        var normalized = email.Trim().ToLowerInvariant();
        if (!IsPlausibleEmail(normalized))
        {
            return "That doesn't look like an email address.";
        }

        if (Current.InvitedEmails.Contains(normalized) || Current.Members.Values.Any(m => m.Email == normalized))
        {
            return "They're already invited or a member.";
        }

        await _client.InviteAsync(Current.Id, normalized);
        await ReloadCurrentAsync();
        return null;
    }

    public async Task CancelInviteAsync(string email)
    {
        if (Current is null || !IsOwner)
        {
            return;
        }

        await _client.CancelInviteAsync(Current.Id, email);
        await ReloadCurrentAsync();
    }

    internal static bool IsPlausibleEmail(string email)
    {
        var at = email.IndexOf('@');
        return at > 0 && at == email.LastIndexOf('@') && email.IndexOf('.', at) > at + 1 && !email.EndsWith('.') && !email.Contains(' ');
    }

    private async Task LoadAsync()
    {
        Error = null;
        var user = _auth.User;
        if (!_auth.IsReady)
        {
            Status = LibraryStatus.Loading;
            return;
        }

        if (user is null)
        {
            Current = null;
            Invites = [];
            _session.SetLibrary(null);
            Status = LibraryStatus.SignedOut;
            return;
        }

        await ForgetOtherAccountAsync(user);

        var cachedId = await _store.GetSettingAsync(LibraryIdKey);
        var cachedName = await _store.GetSettingAsync(LibraryNameKey);

        if (!_connectivity.IsOnline)
        {
            if (cachedId is not null)
            {
                // Offline: trust what this device last knew; members refresh when back online.
                Current ??= new LibraryInfo(cachedId, cachedName ?? DefaultName, "", [user.Uid], [], new Dictionary<string, LibraryMember>());
                _session.SetLibrary(cachedId);
                Status = LibraryStatus.Ready;
            }
            else
            {
                Status = LibraryStatus.OfflineSetup;
            }

            return;
        }

        try
        {
            var mine = await _client.GetMyLibrariesAsync(user.Uid);
            var library = mine.FirstOrDefault(l => l.Id == cachedId) ?? mine.FirstOrDefault();
            if (library is not null)
            {
                await UseLibraryAsync(library);
                return;
            }

            Current = null;
            _session.SetLibrary(null);
            Invites = user.Email is null ? [] : await _client.GetInvitesAsync(user.Email);
            Status = LibraryStatus.NeedsSetup;
        }
        catch (Exception ex)
        {
            if (cachedId is not null)
            {
                Current ??= new LibraryInfo(cachedId, cachedName ?? DefaultName, "", [user.Uid], [], new Dictionary<string, LibraryMember>());
                _session.SetLibrary(cachedId);
                Status = LibraryStatus.Ready;
            }
            else
            {
                Error = ex.Message;
                Status = LibraryStatus.Error;
            }
        }
    }

    /// <summary>
    /// Books on this device belong to whoever last signed in. If a different
    /// account signs in, start clean (their books come down from the cloud).
    /// Books added before sign-in existed are kept and uploaded.
    /// </summary>
    private async Task ForgetOtherAccountAsync(AppUser user)
    {
        var previousUid = await _store.GetSettingAsync(AccountUidKey);
        if (previousUid is not null && previousUid != user.Uid)
        {
            await _store.ClearLibraryDataAsync();
            await _store.RemoveSettingAsync(LibraryIdKey);
            await _store.RemoveSettingAsync(LibraryNameKey);
            await _sync.ResetAsync();
            _books.Invalidate();
            Current = null;
        }

        await _store.SetSettingAsync(AccountUidKey, user.Uid);
    }

    private async Task UseLibraryAsync(LibraryInfo library)
    {
        var previousId = await _store.GetSettingAsync(LibraryIdKey);
        if (previousId is not null && previousId != library.Id)
        {
            // Switching library: pull everything again from the new one.
            await _sync.ResetAsync();
        }

        await _store.SetSettingAsync(LibraryIdKey, library.Id);
        await _store.SetSettingAsync(LibraryNameKey, library.Name);
        Current = library;
        Invites = [];
        Status = LibraryStatus.Ready;
        _session.SetLibrary(library.Id);
    }

    private async Task ReloadCurrentAsync()
    {
        if (Current is not null && await _client.GetLibraryAsync(Current.Id) is { } fresh)
        {
            Current = fresh;
            await _store.SetSettingAsync(LibraryNameKey, fresh.Name);
        }

        Changed?.Invoke();
    }

    private AppUser RequireUser() => _auth.User ?? throw new InvalidOperationException("Not signed in.");

    private async void OnAuthChanged()
    {
        try
        {
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Status = LibraryStatus.Error;
            Changed?.Invoke();
        }
    }

    private async void OnConnectivityChanged(bool isOnline)
    {
        // Finish setup, or refresh members and invites, once back online.
        if (!isOnline || Status is LibraryStatus.Loading or LibraryStatus.SignedOut)
        {
            return;
        }

        try
        {
            await RefreshAsync();
        }
        catch (Exception)
        {
            // LoadAsync records errors; a failed background refresh keeps the current state.
        }
    }

    public void Dispose()
    {
        _auth.Changed -= OnAuthChanged;
        _connectivity.Changed -= OnConnectivityChanged;
        _gate.Dispose();
    }
}

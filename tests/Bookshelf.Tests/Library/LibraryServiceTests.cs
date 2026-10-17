using Bookshelf.Features.Account;
using Bookshelf.Features.Books;
using Bookshelf.Features.Library;
using Bookshelf.Models;
using Bookshelf.Services.Firebase;
using Bookshelf.Services.Sync;
using Bookshelf.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Bookshelf.Tests.Library;

public class LibraryServiceTests
{
    private static readonly AppUser Chris = new("chris", "chris@example.com", "Chris Hill", null);
    private static readonly AppUser Wife = new("wife", "wife@example.com", "Mrs Hill", null);

    private readonly FakeFirebaseClient _client = new();
    private readonly InMemoryLocalStore _store = new();
    private readonly FakeConnectivity _connectivity = new();
    private readonly FakeTimeProvider _time = new(TestBooks.Epoch);
    private readonly RemoteSession _session = new();
    private readonly SyncService _sync;
    private readonly BookService _books;
    private readonly AuthService _auth;
    private readonly LibraryService _sut;

    public LibraryServiceTests()
    {
        var remote = new FirestoreRemoteStore(_client, _session);
        _sync = new SyncService(_store, remote, _connectivity, _time);
        _books = new BookService(_store, _sync, _time);
        _auth = new AuthService(_client);
        _sut = new LibraryService(_auth, _client, _store, _sync, _books, _session, _connectivity);
    }

    private async Task StartAsync(AppUser? restored = null)
    {
        _client.RestoredUser = restored;
        await _sync.InitializeAsync();
        await _auth.InitializeAsync();
        await WaitForAsync(() => _sut.Status != LibraryStatus.Loading);
    }

    [Fact]
    public async Task Signed_out_until_someone_signs_in()
    {
        await StartAsync();

        Assert.Equal(LibraryStatus.SignedOut, _sut.Status);
        Assert.False(_sync.State.RemoteConfigured);
    }

    [Fact]
    public async Task New_user_needs_setup_then_creating_library_uploads_existing_books()
    {
        // A book added in Phase 1, before sign-in existed.
        await _store.PutBookAsync(TestBooks.Make("old", "Emma"), OutboxEntry.ForBook("old", TestBooks.Epoch));
        await StartAsync(Chris);
        Assert.Equal(LibraryStatus.NeedsSetup, _sut.Status);

        await _sut.CreateAsync("The Hill Library");
        await WaitForAsync(() => _sync.State.PendingCount == 0);

        Assert.Equal(LibraryStatus.Ready, _sut.Status);
        Assert.Equal("The Hill Library", _sut.Current!.Name);
        Assert.True(_sut.IsOwner);
        Assert.True(_sync.State.RemoteConfigured);
        Assert.Equal("Emma", Assert.Single(_client.Books[_sut.Current.Id]).Book.Title);
        Assert.Equal(_sut.Current.Id, _store.Settings[LibraryService.LibraryIdKey]);
    }

    [Fact]
    public async Task Blank_name_uses_default()
    {
        await StartAsync(Chris);

        await _sut.CreateAsync("  ");

        Assert.Equal(LibraryService.DefaultName, _sut.Current!.Name);
    }

    [Fact]
    public async Task Existing_member_goes_straight_to_ready_and_pulls_books()
    {
        var library = await _client.CreateLibraryAsync("The Hill Library", Chris);
        await _client.PutBooksAsync(library.Id, [TestBooks.Make("b1", "Persuasion")]);

        await StartAsync(Chris);
        await WaitForAsync(() => _store.Books.ContainsKey("b1"));

        Assert.Equal(LibraryStatus.Ready, _sut.Status);
        Assert.Equal("Persuasion", Assert.Single(await _books.GetAllAsync()).Title);
    }

    [Fact]
    public async Task Invite_then_join_shares_the_library()
    {
        var library = await _client.CreateLibraryAsync("The Hill Library", Chris);
        await _client.PutBooksAsync(library.Id, [TestBooks.Make("b1", "Persuasion")]);
        await _client.InviteAsync(library.Id, Wife.Email!);

        await StartAsync(Wife);
        Assert.Equal(LibraryStatus.NeedsSetup, _sut.Status);
        var invite = Assert.Single(_sut.Invites);
        Assert.Equal("Chris Hill", invite.OwnerName);

        await _sut.AcceptInviteAsync(invite);
        await WaitForAsync(() => _store.Books.ContainsKey("b1"));

        Assert.Equal(LibraryStatus.Ready, _sut.Status);
        Assert.False(_sut.IsOwner);
        Assert.Contains(Wife.Uid, _sut.Current!.MemberUids);
        Assert.Empty(_sut.Current.InvitedEmails);
    }

    [Fact]
    public async Task Owner_can_invite_with_validation()
    {
        await StartAsync(Chris);
        await _sut.CreateAsync("The Hill Library");

        Assert.Null(await _sut.InviteAsync("  Wife@Example.com "));
        Assert.Equal(["wife@example.com"], _sut.Current!.InvitedEmails);
        Assert.NotNull(await _sut.InviteAsync("wife@example.com"));
        Assert.NotNull(await _sut.InviteAsync("chris@example.com"));
        Assert.NotNull(await _sut.InviteAsync("not-an-email"));

        await _sut.CancelInviteAsync("wife@example.com");
        Assert.Empty(_sut.Current.InvitedEmails);
    }

    [Fact]
    public async Task Members_who_are_not_owner_cannot_invite()
    {
        var library = await _client.CreateLibraryAsync("The Hill Library", Chris);
        await _client.InviteAsync(library.Id, Wife.Email!);
        await _client.AcceptInviteAsync(library.Id, Wife);
        await StartAsync(Wife);

        Assert.NotNull(await _sut.InviteAsync("friend@example.com"));
        Assert.Empty(_client.Libraries[library.Id].InvitedEmails);
    }

    [Fact]
    public async Task Offline_start_uses_the_library_remembered_on_this_device()
    {
        _store.Settings[LibraryService.AccountUidKey] = Chris.Uid;
        _store.Settings[LibraryService.LibraryIdKey] = "lib9";
        _store.Settings[LibraryService.LibraryNameKey] = "The Hill Library";
        _connectivity.Set(false);

        await StartAsync(Chris);

        Assert.Equal(LibraryStatus.Ready, _sut.Status);
        Assert.Equal("The Hill Library", _sut.Current!.Name);
        Assert.True(_sync.State.RemoteConfigured);
    }

    [Fact]
    public async Task Offline_first_sign_in_waits_for_connection_then_finishes()
    {
        _connectivity.Set(false);
        await StartAsync(Chris);
        Assert.Equal(LibraryStatus.OfflineSetup, _sut.Status);

        _connectivity.Set(true);
        await WaitForAsync(() => _sut.Status == LibraryStatus.NeedsSetup);
    }

    [Fact]
    public async Task Query_failure_with_remembered_library_still_opens()
    {
        _store.Settings[LibraryService.AccountUidKey] = Chris.Uid;
        _store.Settings[LibraryService.LibraryIdKey] = "lib9";
        _client.QueryError = new Exception("unavailable");

        await StartAsync(Chris);

        Assert.Equal(LibraryStatus.Ready, _sut.Status);
    }

    [Fact]
    public async Task Query_failure_without_remembered_library_shows_error()
    {
        _client.QueryError = new Exception("unavailable");

        await StartAsync(Chris);

        Assert.Equal(LibraryStatus.Error, _sut.Status);
        Assert.Equal("unavailable", _sut.Error);
    }

    [Fact]
    public async Task Different_account_on_same_device_starts_clean()
    {
        _store.Settings[LibraryService.AccountUidKey] = "someone-else";
        _store.Settings[LibraryService.LibraryIdKey] = "their-lib";
        _store.Settings[SyncService.PullCursorKey] = "999";
        await _store.PutBookAsync(TestBooks.Make("theirs"), OutboxEntry.ForBook("theirs", TestBooks.Epoch));

        await StartAsync(Chris);

        Assert.Empty(_store.Books);
        Assert.Empty(_store.Outbox);
        Assert.False(_store.Settings.ContainsKey(SyncService.PullCursorKey));
        Assert.False(_store.Settings.ContainsKey(LibraryService.LibraryIdKey));
        Assert.Equal(Chris.Uid, _store.Settings[LibraryService.AccountUidKey]);
    }

    [Fact]
    public async Task Same_account_signing_back_in_keeps_local_books()
    {
        var library = await _client.CreateLibraryAsync("The Hill Library", Chris);
        _store.Settings[LibraryService.AccountUidKey] = Chris.Uid;
        _store.Settings[LibraryService.LibraryIdKey] = library.Id;
        await _store.PutBookAsync(TestBooks.Make("mine"), OutboxEntry.ForBook("mine", TestBooks.Epoch));

        await StartAsync(Chris);
        await WaitForAsync(() => _sync.State.PendingCount == 0);

        Assert.True(_store.Books.ContainsKey("mine"));
        Assert.Single(_client.Books[library.Id]);
    }

    [Fact]
    public async Task Signing_out_returns_to_sign_in_and_stops_sync()
    {
        await StartAsync(Chris);
        await _sut.CreateAsync("The Hill Library");

        await _auth.SignOutAsync();
        await WaitForAsync(() => _sut.Status == LibraryStatus.SignedOut);

        Assert.Null(_sut.Current);
        Assert.False(_sync.State.RemoteConfigured);
    }

    [Theory]
    [InlineData("wife@example.com", true)]
    [InlineData("a@b.co", true)]
    [InlineData("wife@example", false)]
    [InlineData("@example.com", false)]
    [InlineData("wife@@example.com", false)]
    [InlineData("wife @example.com", false)]
    [InlineData("", false)]
    public void Email_check(string email, bool expected) =>
        Assert.Equal(expected, LibraryService.IsPlausibleEmail(email));

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "Condition was not met in time.");
    }
}

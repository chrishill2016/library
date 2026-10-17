using Bookshelf.Models;
using Bookshelf.Services.Firebase;

namespace Bookshelf.Tests.Fakes;

/// <summary>In-memory stand-in for Firebase Auth + Firestore.</summary>
public sealed class FakeFirebaseClient : IFirebaseClient
{
    private int _nextId;

    public Dictionary<string, LibraryInfo> Libraries { get; } = [];
    public Dictionary<string, List<RemoteBook>> Books { get; } = [];
    public List<long?> ChangeQueries { get; } = [];

    /// <summary>The account the next sign-in "chooses".</summary>
    public AppUser? NextSignIn { get; set; }

    /// <summary>The account already signed in when the app starts.</summary>
    public AppUser? RestoredUser { get; set; }

    public Exception? SignInError { get; set; }
    public Exception? QueryError { get; set; }
    public long Clock { get; set; } = 1_000_000;

    public event Action<AppUser?>? AuthStateChanged;

    public Task InitializeAsync()
    {
        AuthStateChanged?.Invoke(RestoredUser);
        return Task.CompletedTask;
    }

    public Task SignInWithGoogleAsync()
    {
        if (SignInError is not null)
        {
            throw SignInError;
        }

        AuthStateChanged?.Invoke(NextSignIn);
        return Task.CompletedTask;
    }

    public Task SignOutAsync()
    {
        AuthStateChanged?.Invoke(null);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LibraryInfo>> GetMyLibrariesAsync(string uid)
    {
        if (QueryError is not null)
        {
            throw QueryError;
        }

        return Task.FromResult<IReadOnlyList<LibraryInfo>>([.. Libraries.Values.Where(l => l.MemberUids.Contains(uid))]);
    }

    public Task<IReadOnlyList<LibraryInfo>> GetInvitesAsync(string email) =>
        Task.FromResult<IReadOnlyList<LibraryInfo>>([.. Libraries.Values.Where(l => l.InvitedEmails.Contains(email))]);

    public Task<LibraryInfo?> GetLibraryAsync(string libraryId) => Task.FromResult(Libraries.GetValueOrDefault(libraryId));

    public Task<LibraryInfo> CreateLibraryAsync(string name, AppUser owner)
    {
        var library = new LibraryInfo($"lib{++_nextId}", name, owner.Uid, [owner.Uid], [],
            new Dictionary<string, LibraryMember> { [owner.Uid] = new(owner.Name, owner.Email ?? "") });
        Libraries[library.Id] = library;
        return Task.FromResult(library);
    }

    public Task AcceptInviteAsync(string libraryId, AppUser user)
    {
        var library = Libraries[libraryId];
        if (!library.InvitedEmails.Contains(user.Email))
        {
            throw new InvalidOperationException("PERMISSION_DENIED");
        }

        Libraries[libraryId] = library with
        {
            MemberUids = [.. library.MemberUids, user.Uid],
            InvitedEmails = [.. library.InvitedEmails.Where(e => e != user.Email)],
            Members = new Dictionary<string, LibraryMember>(library.Members) { [user.Uid] = new(user.Name, user.Email ?? "") },
        };
        return Task.CompletedTask;
    }

    public Task InviteAsync(string libraryId, string email)
    {
        var library = Libraries[libraryId];
        Libraries[libraryId] = library with { InvitedEmails = [.. library.InvitedEmails, email] };
        return Task.CompletedTask;
    }

    public Task CancelInviteAsync(string libraryId, string email)
    {
        var library = Libraries[libraryId];
        Libraries[libraryId] = library with { InvitedEmails = [.. library.InvitedEmails.Where(e => e != email)] };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RemoteBook>> GetBookChangesAsync(string libraryId, long? sinceMillis)
    {
        ChangeQueries.Add(sinceMillis);
        var books = Books.GetValueOrDefault(libraryId) ?? [];
        return Task.FromResult<IReadOnlyList<RemoteBook>>([.. books.Where(b => sinceMillis is null || b.SyncedAt > sinceMillis).OrderBy(b => b.SyncedAt)]);
    }

    public Task PutBooksAsync(string libraryId, IReadOnlyList<Book> books)
    {
        var list = Books.TryGetValue(libraryId, out var existing) ? existing : Books[libraryId] = [];
        foreach (var book in books)
        {
            list.RemoveAll(b => b.Book.Id == book.Id);
            list.Add(new RemoteBook(book, Clock += 1000));
        }

        return Task.CompletedTask;
    }
}

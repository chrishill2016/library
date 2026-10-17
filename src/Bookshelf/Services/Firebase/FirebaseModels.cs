using Bookshelf.Models;
using Microsoft.Extensions.Configuration;

namespace Bookshelf.Services.Firebase;

/// <summary>The Firebase web config from <c>wwwroot/appsettings.json</c>. Not a secret.</summary>
/// <param name="EmulatorHost">
/// Local testing only: when set (e.g. "127.0.0.1"), Auth and Firestore use the Firebase emulators.
/// </param>
public sealed record FirebaseOptions(
    string ApiKey,
    string AuthDomain,
    string ProjectId,
    string StorageBucket,
    string MessagingSenderId,
    string AppId,
    string? EmulatorHost = null)
{
    public static FirebaseOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Firebase");
        string Get(string key) => section[key] ?? throw new InvalidOperationException($"Missing Firebase:{key} in appsettings.json.");
        return new(Get(nameof(ApiKey)), Get(nameof(AuthDomain)), Get(nameof(ProjectId)),
                   Get(nameof(StorageBucket)), Get(nameof(MessagingSenderId)), Get(nameof(AppId)),
                   section[nameof(EmulatorHost)]);
    }
}

/// <summary>The signed-in Google account. Email is lower-cased.</summary>
public sealed record AppUser(string Uid, string? Email, string? DisplayName, string? PhotoUrl)
{
    public string Name => DisplayName ?? Email ?? "You";
}

public sealed record LibraryMember(string Name, string Email);

public sealed record LibraryInfo(
    string Id,
    string Name,
    string OwnerUid,
    IReadOnlyList<string> MemberUids,
    IReadOnlyList<string> InvitedEmails,
    IReadOnlyDictionary<string, LibraryMember> Members)
{
    public string OwnerName => Members.TryGetValue(OwnerUid, out var owner) ? owner.Name : "someone";
}

/// <summary>A book as read from Firestore, with its server write time.</summary>
public sealed record RemoteBook(Book Book, long SyncedAt);

/// <summary>A reading as read from Firestore, with its server write time.</summary>
public sealed record RemoteReading(Reading Reading, long SyncedAt);

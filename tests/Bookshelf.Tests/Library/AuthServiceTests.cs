using Bookshelf.Features.Account;
using Bookshelf.Services.Firebase;
using Bookshelf.Tests.Fakes;

namespace Bookshelf.Tests.Library;

public class AuthServiceTests
{
    private readonly FakeFirebaseClient _client = new();

    [Fact]
    public async Task Restores_saved_user_and_becomes_ready()
    {
        _client.RestoredUser = new AppUser("u", "u@example.com", "U", null);
        var sut = new AuthService(_client);

        await sut.InitializeAsync();

        Assert.True(sut.IsReady);
        Assert.Equal("u", sut.User!.Uid);
    }

    [Fact]
    public async Task Sign_in_and_out_update_user()
    {
        var sut = new AuthService(_client);
        await sut.InitializeAsync();
        _client.NextSignIn = new AppUser("u", "u@example.com", null, null);

        Assert.Null(await sut.SignInAsync());
        Assert.Equal("u@example.com", sut.User!.Name);

        await sut.SignOutAsync();
        Assert.Null(sut.User);
    }

    [Theory]
    [InlineData("Firebase: Error (auth/popup-closed-by-user).", null)]
    [InlineData("Firebase: Error (auth/popup-blocked).", "The sign-in window was blocked. Please allow pop-ups and try again.")]
    [InlineData("Firebase: Error (auth/network-request-failed).", "You need to be online to sign in.")]
    public async Task Sign_in_errors_are_friendly(string raw, string? expected)
    {
        var sut = new AuthService(_client);
        _client.SignInError = new Exception(raw);

        Assert.Equal(expected, await sut.SignInAsync());
    }
}

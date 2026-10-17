using Bookshelf.Services.Firebase;

namespace Bookshelf.Features.Account;

/// <summary>Tracks the signed-in Google account.</summary>
public sealed class AuthService : IDisposable
{
    private readonly IFirebaseClient _client;
    private Task? _initialization;

    public AuthService(IFirebaseClient client)
    {
        _client = client;
        _client.AuthStateChanged += OnAuthStateChanged;
    }

    /// <summary>True once the saved sign-in (if any) has been restored.</summary>
    public bool IsReady { get; private set; }

    public AppUser? User { get; private set; }

    /// <summary>Set if Firebase could not start (e.g. first ever launch while offline).</summary>
    public string? StartupError { get; private set; }

    public event Action? Changed;

    public Task InitializeAsync() => _initialization ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        try
        {
            await _client.InitializeAsync();
        }
        catch (Exception ex)
        {
            StartupError = ex.Message;
        }

        IsReady = true;
        Changed?.Invoke();
    }

    /// <summary>Opens the Google sign-in popup. Returns an error message, or null on success.</summary>
    public async Task<string?> SignInAsync()
    {
        try
        {
            await _client.SignInWithGoogleAsync();
            return null;
        }
        catch (Exception ex)
        {
            return FriendlyError(ex.Message);
        }
    }

    public Task SignOutAsync() => _client.SignOutAsync();

    internal static string? FriendlyError(string message) => message switch
    {
        _ when message.Contains("popup-closed-by-user") || message.Contains("cancelled-popup-request") => null,
        _ when message.Contains("popup-blocked") => "The sign-in window was blocked. Please allow pop-ups and try again.",
        _ when message.Contains("network-request-failed") => "You need to be online to sign in.",
        _ when message.Contains("unauthorized-domain") => "This web address isn't allowed to sign in yet (Firebase authorised domains).",
        _ => $"Sign-in failed: {message}",
    };

    private void OnAuthStateChanged(AppUser? user)
    {
        if (user == User)
        {
            return;
        }

        User = user;
        Changed?.Invoke();
    }

    public void Dispose() => _client.AuthStateChanged -= OnAuthStateChanged;
}

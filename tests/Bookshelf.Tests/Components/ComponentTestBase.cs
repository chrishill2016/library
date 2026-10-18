using Bookshelf.Features.Account;
using Bookshelf.Features.Books;
using Bookshelf.Features.Dashboard;
using Bookshelf.Features.Library;
using Bookshelf.Features.Lookup;
using Bookshelf.Features.Photos;
using Bookshelf.Features.ReadingLog;
using Bookshelf.Features.Scan;
using Bookshelf.Services.Browser;
using Bookshelf.Services.Firebase;
using Bookshelf.Services.Storage;
using Bookshelf.Services.Sync;
using Bookshelf.Tests.Fakes;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MudBlazor.Services;

namespace Bookshelf.Tests.Components;

/// <remarks>
/// MudBlazor services are async-disposable only, so the context must be
/// disposed via IAsyncLifetime (xUnit v2 otherwise calls Dispose).
/// </remarks>
public abstract class ComponentTestBase : BunitContext, IAsyncLifetime
{
    protected InMemoryLocalStore Store { get; } = new();
    protected FakeRemoteStore Remote { get; } = new() { IsConfigured = false };
    protected FakeConnectivity Connectivity { get; } = new();
    protected FakeTimeProvider Time { get; } = new(TestBooks.Epoch.AddDays(1));
    protected FakeFirebaseClient Firebase { get; } = new();
    protected FakeBookLookup Lookup { get; } = new();
    protected FakeBarcodeScanner Scanner { get; } = new();
    protected FakePhotoStore Photos { get; } = new();

    protected ComponentTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<TimeProvider>(Time);
        Services.AddSingleton<ISyncService>(_ => new SyncService(Store, Remote, Connectivity, Time));
        Services.AddSingleton(sp => new BookService(Store, sp.GetRequiredService<ISyncService>(), Time));
        Services.AddSingleton<ILocalStore>(Store);
        Services.AddSingleton<IConnectivityService>(Connectivity);
        Services.AddSingleton<IFirebaseClient>(Firebase);
        Services.AddSingleton<RemoteSession>();
        Services.AddSingleton<AuthService>();
        Services.AddSingleton<LibraryService>();
        Services.AddSingleton<IBookLookup>(Lookup);
        Services.AddSingleton<PendingLookupService>();
        Services.AddSingleton(sp => new BookDetailsFiller(sp.GetRequiredService<BookService>(), Lookup, Connectivity) { Pause = TimeSpan.Zero });
        Services.AddSingleton<ScanService>();
        Services.AddSingleton<IBarcodeScanner>(Scanner);
        Services.AddSingleton<IPhotoStore>(Photos);
        Services.AddSingleton<PhotoService>();
        Services.AddSingleton(new DashboardPreferences(Store));
        Services.AddSingleton(sp => new ReadingService(Store, sp.GetRequiredService<ISyncService>(), sp.GetRequiredService<AuthService>(), Time));
    }

    /// <summary>Signs in as this user (as if restored on startup), for pages that need someone signed in.</summary>
    protected async Task SignInAsync(string uid = "chris", string name = "Chris")
    {
        Firebase.RestoredUser = new AppUser(uid, $"{uid}@example.com", name, null);
        await Services.GetRequiredService<AuthService>().InitializeAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => base.DisposeAsync().AsTask();
}

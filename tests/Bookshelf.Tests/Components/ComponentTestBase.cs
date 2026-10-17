using Bookshelf.Features.Account;
using Bookshelf.Features.Books;
using Bookshelf.Features.Library;
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
    }

    public Task InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => base.DisposeAsync().AsTask();
}

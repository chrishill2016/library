using Bookshelf;
using Bookshelf.Features.Books;
using Bookshelf.Services.Browser;
using Bookshelf.Services.Storage;
using Bookshelf.Services.Sync;
using Bookshelf.Theming;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomCenter;
    config.SnackbarConfiguration.VisibleStateDuration = 4000;
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ILocalStore, IndexedDbLocalStore>();
builder.Services.AddSingleton<IConnectivityService, BrowserConnectivityService>();
builder.Services.AddSingleton<AppUpdateService>();
// Phase 2 swaps this for a Firestore-backed store.
builder.Services.AddSingleton<IRemoteStore, NotConfiguredRemoteStore>();
builder.Services.AddSingleton<ISyncService, SyncService>();
builder.Services.AddSingleton<BookService>();
builder.Services.AddSingleton<ThemeService>();

await builder.Build().RunAsync();

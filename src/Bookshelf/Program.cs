using Bookshelf;
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
builder.Services.AddSingleton(FirebaseOptions.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton<IFirebaseClient, FirebaseClient>();
builder.Services.AddSingleton<RemoteSession>();
builder.Services.AddSingleton<IRemoteStore, FirestoreRemoteStore>();
builder.Services.AddSingleton<ISyncService, SyncService>();
builder.Services.AddSingleton<BookService>();
builder.Services.AddSingleton<ReadingService>();
builder.Services.AddSingleton<DashboardPreferences>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<LibraryService>();
builder.Services.AddSingleton<ThemeService>();

// Book lookup: Google Books first (better UK coverage), Open Library fills gaps.
builder.Services.AddSingleton(LookupOptions.FromConfiguration(builder.Configuration));
var lookupHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
builder.Services.AddSingleton<IBookSource>(sp => new GoogleBooksSource(lookupHttp, sp.GetRequiredService<LookupOptions>()));
builder.Services.AddSingleton<IBookSource>(_ => new OpenLibrarySource(lookupHttp));
builder.Services.AddSingleton<IBookLookup, BookLookupService>();
builder.Services.AddSingleton<PendingLookupService>();
builder.Services.AddSingleton<ScanService>();
builder.Services.AddSingleton<IBarcodeScanner, BarcodeScanner>();
builder.Services.AddSingleton<IPhotoStore, JsPhotoStore>();
builder.Services.AddSingleton<PhotoService>();

await builder.Build().RunAsync();

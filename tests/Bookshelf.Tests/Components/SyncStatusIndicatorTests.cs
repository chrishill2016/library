using Bookshelf.Components;
using Bookshelf.Models;
using Bookshelf.Services.Sync;
using Bookshelf.Tests.Fakes;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Tests.Components;

public class SyncStatusIndicatorTests : ComponentTestBase
{
    [Fact]
    public async Task Shows_on_device_with_pending_count_when_cloud_not_configured()
    {
        await Store.PutBookAsync(TestBooks.Make("a"), OutboxEntry.ForBook("a", TestBooks.Epoch));
        await Services.GetRequiredService<ISyncService>().InitializeAsync();

        var cut = Render<SyncStatusIndicator>();

        Assert.Contains("On device", cut.Find("[data-testid=sync-status]").TextContent);
        Assert.Contains("1", cut.Find(".mud-badge").TextContent);
        Assert.Contains("1 change waiting to sync", string.Join("|", cut.Instance.Details));
    }

    [Fact]
    public async Task Switches_to_offline_when_connection_drops()
    {
        await Services.GetRequiredService<ISyncService>().InitializeAsync();
        var cut = Render<SyncStatusIndicator>();

        Connectivity.Set(false);

        cut.WaitForAssertion(() => Assert.Contains("Offline", cut.Find("[data-testid=sync-status]").TextContent));
    }

    [Fact]
    public async Task Shows_synced_and_last_sync_time_when_up_to_date()
    {
        Remote.IsConfigured = true;
        var sync = Services.GetRequiredService<ISyncService>();
        await sync.InitializeAsync();
        Time.Advance(TimeSpan.FromMinutes(5));

        var cut = Render<SyncStatusIndicator>();

        Assert.Contains("Synced", cut.Find("[data-testid=sync-status]").TextContent);
        Assert.Contains("Last synced 5 min ago", cut.Instance.Details);
    }
}

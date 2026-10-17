using Bookshelf.Features.Account;
using Bookshelf.Features.Library;
using Bookshelf.Services.Firebase;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Tests.Components;

public class LibrarySettingsTests : ComponentTestBase
{
    private static readonly AppUser Chris = new("chris", "chris@example.com", "Chris Hill", null);
    private static readonly AppUser Wife = new("wife", "wife@example.com", "Mrs Hill", null);

    private async Task<LibraryInfo> SignInAsync(AppUser user, Func<Task<LibraryInfo>> arrange)
    {
        var library = await arrange();
        Firebase.RestoredUser = user;
        var service = Services.GetRequiredService<LibraryService>();
        await Services.GetRequiredService<AuthService>().InitializeAsync();
        for (var i = 0; i < 100 && service.Status != LibraryStatus.Ready; i++)
        {
            await Task.Delay(10);
        }

        Assert.Equal(LibraryStatus.Ready, service.Status);
        return library;
    }

    [Fact]
    public async Task Owner_sees_members_and_can_invite()
    {
        var library = await SignInAsync(Chris, () => Firebase.CreateLibraryAsync("The Hill Library", Chris));
        var cut = Render<LibrarySettings>();

        Assert.Equal("The Hill Library", cut.Find("[data-testid=library-title]").TextContent);
        Assert.Contains("Chris Hill", cut.Find("[data-testid=members]").TextContent);
        Assert.Contains("Owner", cut.Find("[data-testid=members]").TextContent);

        cut.Find("input[data-testid=invite-email]").Change("wife@example.com");
        cut.Find("[data-testid=invite-button]").Click();

        cut.WaitForAssertion(() => Assert.Contains("wife@example.com", cut.Find("[data-testid=invited]").TextContent));
        Assert.Equal(["wife@example.com"], Firebase.Libraries[library.Id].InvitedEmails);
    }

    [Fact]
    public async Task Members_who_are_not_owner_cannot_invite()
    {
        await SignInAsync(Wife, async () =>
        {
            var library = await Firebase.CreateLibraryAsync("The Hill Library", Chris);
            await Firebase.InviteAsync(library.Id, Wife.Email!);
            await Firebase.AcceptInviteAsync(library.Id, Wife);
            return library;
        });

        var cut = Render<LibrarySettings>();

        Assert.Contains("Mrs Hill", cut.Find("[data-testid=members]").TextContent);
        Assert.Empty(cut.FindAll("[data-testid=invite-button]"));
    }

    [Fact]
    public async Task Invite_is_disabled_offline()
    {
        await SignInAsync(Chris, () => Firebase.CreateLibraryAsync("The Hill Library", Chris));
        var cut = Render<LibrarySettings>();

        Connectivity.Set(false);

        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=invite-button]").HasAttribute("disabled")));
    }
}

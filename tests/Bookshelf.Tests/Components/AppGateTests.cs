using Bookshelf.Components;
using Bookshelf.Features.Account;
using Bookshelf.Features.Library;
using Bookshelf.Services.Firebase;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Tests.Components;

public class AppGateTests : ComponentTestBase
{
    private static readonly AppUser Chris = new("chris", "chris@example.com", "Chris Hill", null);
    private static readonly AppUser Wife = new("wife", "wife@example.com", "Mrs Hill", null);

    private IRenderedComponent<AppGate> RenderGate() =>
        Render<AppGate>(p => p.AddChildContent("<p data-testid=\"app-content\">the shelf</p>"));

    private async Task StartAsync(AppUser? restored)
    {
        Firebase.RestoredUser = restored;
        // Resolve LibraryService first so it is listening for auth changes.
        Services.GetRequiredService<LibraryService>();
        await Services.GetRequiredService<AuthService>().InitializeAsync();
    }

    [Fact]
    public void Shows_loading_before_sign_in_is_restored()
    {
        var cut = RenderGate();

        Assert.NotNull(cut.Find("[data-testid=gate-loading]"));
        Assert.Empty(cut.FindAll("[data-testid=app-content]"));
    }

    [Fact]
    public async Task Requires_sign_in_and_hides_the_app()
    {
        var cut = RenderGate();
        await StartAsync(null);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=sign-in]")));
        Assert.Empty(cut.FindAll("[data-testid=app-content]"));
    }

    [Fact]
    public async Task Signing_in_leads_to_setup_and_creating_opens_the_app()
    {
        var cut = RenderGate();
        await StartAsync(null);
        Firebase.NextSignIn = Chris;

        cut.WaitForElement("[data-testid=sign-in-button]").Click();
        cut.WaitForAssertion(() => Assert.Contains("Welcome, Chris Hill", cut.Markup));
        Assert.Equal(LibraryService.DefaultName, cut.Find("input[data-testid=library-name]").GetAttribute("value"));

        cut.Find("[data-testid=create-library]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=app-content]")));
        Assert.Equal(LibraryService.DefaultName, Assert.Single(Firebase.Libraries.Values).Name);
    }

    [Fact]
    public async Task Invited_user_sees_invitation_and_can_join()
    {
        var library = await Firebase.CreateLibraryAsync("The Hill Library", Chris);
        await Firebase.InviteAsync(library.Id, Wife.Email!);
        var cut = RenderGate();

        await StartAsync(Wife);

        cut.WaitForAssertion(() => Assert.Contains("Invited by Chris Hill", cut.Find("[data-testid=invite]").TextContent));
        cut.Find("[data-testid=invite] button").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=app-content]")));
        Assert.Contains(Wife.Uid, Firebase.Libraries[library.Id].MemberUids);
    }

    [Fact]
    public async Task Sign_in_error_is_shown()
    {
        var cut = RenderGate();
        await StartAsync(null);
        Firebase.SignInError = new Exception("Firebase: Error (auth/network-request-failed).");

        cut.WaitForElement("[data-testid=sign-in-button]").Click();

        cut.WaitForAssertion(() =>
            Assert.Equal("You need to be online to sign in.", cut.Find("[data-testid=sign-in-error]").TextContent.Trim()));
    }
}

using Bookshelf.Features.Books;
using Bookshelf.Features.Dashboard;
using Bookshelf.Features.Lookup;
using Bookshelf.Features.Settings;
using Bookshelf.Models;
using Bookshelf.Tests.Fakes;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Tests.Components;

public class SynopsisAndGenreTests : ComponentTestBase
{
    private const string Hobbit = "9780261102217";

    [Fact]
    public async Task Book_page_shows_genres_and_a_long_synopsis_with_read_more()
    {
        var synopsis = string.Join("\n\n", Enumerable.Repeat("Bilbo goes there and back again with thirteen dwarves and a wizard.", 8));
        await Store.PutBookAsync(TestBooks.Make("h", "The Hobbit") with { Synopsis = synopsis, Genres = ["fantasy", "children"] });

        var cut = Render<BookDetailsPage>(p => p.Add(x => x.Id, "h"));

        var genres = cut.WaitForElement("[data-testid=book-genres]");
        Assert.Equal(["Fantasy", "Children's"], genres.QuerySelectorAll("a").Select(a => a.TextContent));
        Assert.Equal("./?genre=fantasy", genres.QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal(8, cut.FindAll("[data-testid=synopsis] p").Count);
        Assert.DoesNotContain("open", cut.Find("[data-testid=synopsis]").ClassList);

        cut.Find("[data-testid=synopsis-more]").Click();

        Assert.Contains("open", cut.Find("[data-testid=synopsis]").ClassList);
        Assert.Contains("Show less", cut.Find("[data-testid=synopsis-more]").TextContent);
    }

    [Fact]
    public async Task Short_synopsis_needs_no_read_more()
    {
        await Store.PutBookAsync(TestBooks.Make("e", "Emma") with { Synopsis = "Matchmaking goes awry." });

        var cut = Render<BookDetailsPage>(p => p.Add(x => x.Id, "e"));

        cut.WaitForElement("[data-testid=synopsis]");
        Assert.Empty(cut.FindAll("[data-testid=synopsis-more]"));
        Assert.Empty(cut.FindAll("[data-testid=book-genres]"));
    }

    [Fact]
    public async Task Edit_form_picks_up_to_three_genres_and_saves_the_synopsis()
    {
        await Store.PutBookAsync(TestBooks.Make("e", "Emma"));
        var cut = Render<BookEditPage>(p => p.Add(x => x.Id, "e"));

        cut.WaitForElement("[data-testid=genre-classics]").Click();
        cut.Find("[data-testid=genre-romance]").Click();
        cut.Find("[data-testid=genre-humour]").Click();
        Assert.True(cut.Find("[data-testid=genre-crime]").HasAttribute("disabled"));
        cut.Find("[data-testid=genre-humour]").Click();
        Assert.False(cut.Find("[data-testid=genre-crime]").HasAttribute("disabled"));
        cut.Find("textarea[data-testid=synopsis]").Change("  Matchmaking goes awry.  ");
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.Equal(["classics", "romance"], Store.Books["e"].Genres));
        Assert.Equal("Matchmaking goes awry.", Store.Books["e"].Synopsis);
    }

    [Fact]
    public void Isbn_lookup_on_the_add_form_fills_synopsis_and_suggests_genres()
    {
        Lookup.Known[Hobbit] = new BookCandidate("The Hobbit", "J. R. R. Tolkien", Hobbit, null, 1937, null, "Fake")
        {
            Synopsis = "There and back again.",
            Categories = ["Juvenile Fiction / Fantasy & Magic"],
        };
        var cut = Render<BookEditPage>();

        cut.Find("input[data-testid=isbn]").Input(Hobbit);
        cut.Find("[data-testid=isbn] ~ * button, .mud-input-adornment-end button").Click();

        cut.WaitForAssertion(() => Assert.Equal("There and back again.", cut.Find("textarea[data-testid=synopsis]").GetAttribute("value") ?? cut.Find("textarea[data-testid=synopsis]").TextContent));
        Assert.Equal("true", cut.Find("[data-testid=genre-fantasy]").GetAttribute("aria-pressed"));
        Assert.Equal("true", cut.Find("[data-testid=genre-children]").GetAttribute("aria-pressed"));
    }

    [Fact]
    public async Task Shelf_opens_filtered_to_a_genre_from_the_address()
    {
        await Store.PutBookAsync(TestBooks.Make("h", "The Hobbit") with { Genres = ["fantasy", "children"] });
        await Store.PutBookAsync(TestBooks.Make("e", "Emma") with { Genres = ["classics"] });
        await Store.PutBookAsync(TestBooks.Make("m", "Middlemarch"));
        Services.GetRequiredService<NavigationManager>().NavigateTo("./?genre=fantasy");

        var cut = Render<ShelfPage>();

        cut.WaitForAssertion(() => Assert.Equal(["The Hobbit"], cut.FindAll(".book-row-title").Select(e => e.TextContent.Trim())));
        Assert.Equal("1 of 3 volumes", cut.Find("[data-testid=book-count]").TextContent.Trim());
    }

    [Fact]
    public async Task Genre_filter_lists_only_genres_on_the_shelf_with_counts()
    {
        await Store.PutBookAsync(TestBooks.Make("h", "The Hobbit") with { Genres = ["fantasy", "children"] });
        await Store.PutBookAsync(TestBooks.Make("f", "Fellowship") with { Genres = ["fantasy"] });
        await Store.PutBookAsync(TestBooks.Make("w", "Wanted") with { Genres = ["horror"], OnWishlist = true });

        var cut = Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ShelfPage>(1);
            builder.CloseComponent();
        });

        await cut.FindComponent<MudBlazor.MudSelect<string>>().InvokeAsync(() => cut.FindComponent<MudBlazor.MudSelect<string>>().Instance.OpenMenu());
        cut.WaitForAssertion(() => Assert.Equal(
            ["Fantasy (2)", "Children's (1)"],
            cut.FindAll(".mud-popover .mud-list-item").Select(i => i.TextContent.Trim())));
    }

    [Fact]
    public async Task Search_also_matches_synopsis_and_genre_names()
    {
        await Store.PutBookAsync(TestBooks.Make("h", "The Hobbit") with { Synopsis = "A dragon guards the gold.", Genres = ["fantasy"] });
        await Store.PutBookAsync(TestBooks.Make("e", "Emma") with { Genres = ["classics"] });

        Assert.Equal(["h"], BookSearch.Filter(Store.Books.Values, "dragon").Select(b => b.Id));
        Assert.Equal(["e"], BookSearch.Filter(Store.Books.Values, "classics").Select(b => b.Id));
    }

    [Fact]
    public async Task Settings_fills_in_missing_details()
    {
        Services.AddSingleton(new Bookshelf.Theming.ThemeService(Store));
        await SignInAsync();
        Lookup.Known[Hobbit] = new BookCandidate("The Hobbit", "J. R. R. Tolkien", Hobbit, null, 1937, null, "Fake")
        {
            Synopsis = "There and back again.",
            Categories = ["Fiction / Fantasy / General"],
        };
        await Store.PutBookAsync(TestBooks.Make("h", "The Hobbit") with { Isbn = Hobbit });
        var cut = Render<SettingsPage>();
        Assert.Contains("1 book is missing", cut.WaitForElement("[data-testid=fill-missing]").TextContent);

        cut.Find("[data-testid=fill-start]").Click();

        cut.WaitForAssertion(() => Assert.Contains("filled in 1 of the 1 books checked", cut.Find("[data-testid=fill-result]").TextContent));
        Assert.Contains("Every book has a synopsis and genres", cut.Find("[data-testid=fill-missing]").TextContent);
        Assert.Equal(["fantasy"], Store.Books["h"].Genres);
    }

    [Fact]
    public async Task Dashboard_shows_favourite_genres_of_books_read()
    {
        await SignInAsync();
        await Store.PutBookAsync(TestBooks.Make("h", "The Hobbit") with { Genres = ["fantasy", "children"] });
        await Store.PutBookAsync(TestBooks.Make("f", "Fellowship") with { Genres = ["fantasy"] });
        await Store.PutBookAsync(TestBooks.Make("e", "Emma") with { Genres = ["classics"] });
        await Store.PutReadingAsync(TestReadings.Make("h", "chris", status: ReadingStatus.Read));
        await Store.PutReadingAsync(TestReadings.Make("f", "chris", status: ReadingStatus.Read));

        var cut = Render<DashboardPage>();

        var genres = cut.WaitForElement("[data-testid=top-genres]");
        Assert.Equal(["Fantasy2 read", "Children's1 read"], genres.QuerySelectorAll("li").Select(li => li.TextContent.Trim()));
        Assert.Contains("The genre you read most is Fantasy", cut.Find("[data-testid=fun-facts]").TextContent);
    }
}

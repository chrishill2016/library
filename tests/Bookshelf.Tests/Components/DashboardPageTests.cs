using Bookshelf.Features.Books;
using Bookshelf.Features.Dashboard;
using Bookshelf.Features.Settings;
using Bookshelf.Models;
using Bookshelf.Tests.Fakes;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Tests.Components;

/// <remarks>The test clock is 2 January 2026 (TestBooks.Epoch + 1 day).</remarks>
public class DashboardPageTests : ComponentTestBase
{
    private async Task SeedAsync()
    {
        await SignInAsync();
        await Store.PutBookAsync(TestBooks.Make("e", "Emma", "Jane Austen"));
        await Store.PutBookAsync(TestBooks.Make("p", "Persuasion", "Jane Austen"));
        await Store.PutBookAsync(TestBooks.Make("h", "The Hobbit", "J. R. R. Tolkien"));
        await Store.PutReadingAsync(TestReadings.Make("e", "chris", rating: 5, status: ReadingStatus.Read, reads: new ReadDate(2026, 1, 1)));
        await Store.PutReadingAsync(TestReadings.Make("p", "chris", rating: 4, reads: new ReadDate(2025, 12, 30)));
        await Store.PutReadingAsync(TestReadings.Make("h", "chris", status: ReadingStatus.Reading));
        // Someone else's reading doesn't count: these are my stats.
        await Store.PutReadingAsync(TestReadings.Make("h", "wife", rating: 1, reads: new ReadDate(2026, 1, 1)));
    }

    [Fact]
    public async Task Shows_my_headline_numbers()
    {
        await SeedAsync();

        var cut = Render<DashboardPage>();

        cut.WaitForAssertion(() => Assert.Contains("1", cut.Find(".stat-hero-value").TextContent));
        Assert.Contains("finished in 2026", cut.Find("[data-testid=hero]").TextContent);
        Assert.Contains("Emma", cut.Find("[data-testid=hero]").TextContent);
        Assert.Equal("3", cut.Find("[data-testid=tile-shelf] .stat-value").TextContent.Trim());
        Assert.Equal("2", cut.Find("[data-testid=tile-read] .stat-value").TextContent.Trim());
        Assert.Contains("2 books rated, averaging 4.5 stars", cut.Find("[data-testid=ratings]").TextContent);
        Assert.Contains("Jane Austen", cut.Find("[data-testid=top-authors]").TextContent);
        Assert.Contains("Emma", cut.Find("[data-testid=five-stars]").TextContent);
    }

    [Fact]
    public async Task Tapping_a_year_bar_shows_that_years_books()
    {
        await SeedAsync();
        var cut = Render<DashboardPage>();
        cut.WaitForAssertion(() => Assert.Contains("2026 in books", cut.Find("[data-testid=year-in-books]").TextContent));

        cut.FindAll("[data-testid=year-bar]").First(b => b.GetAttribute("aria-label")!.StartsWith("2025")).Click();

        var year = cut.Find("[data-testid=year-in-books]");
        Assert.Contains("2025 in books", year.TextContent);
        Assert.Contains("Persuasion", year.TextContent);
        Assert.DoesNotContain("Emma", year.TextContent);
        Assert.Contains("Favourite: Persuasion", cut.Find("[data-testid=year-summary]").TextContent);
    }

    [Fact]
    public async Task Last_12_months_setting_changes_the_headline()
    {
        await SeedAsync();
        await Services.GetRequiredService<DashboardPreferences>().SetPeriodAsync(StatsPeriod.Last12Months);

        var cut = Render<DashboardPage>();

        cut.WaitForAssertion(() => Assert.Equal("2", cut.Find(".stat-hero-value").TextContent.Trim()));
        Assert.Contains("in the last 12 months", cut.Find("[data-testid=hero]").TextContent);
    }

    [Fact]
    public async Task Invites_reading_data_when_there_is_none()
    {
        await SignInAsync();
        await Store.PutBookAsync(TestBooks.Make("e", "Emma"));

        var cut = Render<DashboardPage>();

        cut.WaitForElement("[data-testid=no-reading]");
        Assert.Empty(cut.FindAll("[data-testid=year-chart]"));
    }

    [Fact]
    public async Task Shelf_summary_links_to_the_dashboard_and_follows_the_setting()
    {
        await SeedAsync();
        var cut = Render<ShelfPage>();

        var summary = cut.WaitForElement("[data-testid=shelf-summary]");
        Assert.Equal("dashboard", summary.GetAttribute("href"));
        Assert.Contains("1 read in 2026", summary.TextContent);
        Assert.Contains("1 reading now", summary.TextContent);

        await Services.GetRequiredService<DashboardPreferences>().SetPeriodAsync(StatsPeriod.Last12Months);

        cut.WaitForAssertion(() => Assert.Contains("2 read in the last 12 months", cut.Find("[data-testid=shelf-summary]").TextContent));
    }

    [Fact]
    public async Task Settings_toggle_saves_the_period()
    {
        Services.AddSingleton(new Bookshelf.Theming.ThemeService(Store));
        await SignInAsync();
        var cut = Render<SettingsPage>();

        cut.Find("input[data-testid=period-rolling]").Click();

        cut.WaitForAssertion(() => Assert.Equal("Last12Months", Store.Settings[DashboardPreferences.PeriodKey]));
    }
}

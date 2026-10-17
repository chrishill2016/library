using Bookshelf.Features.Books;
using Bookshelf.Tests.Fakes;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Tests.Components;

public class ShelfPageTests : ComponentTestBase
{
    [Fact]
    public void Empty_shelf_invites_adding_a_book()
    {
        var cut = Render<ShelfPage>();

        Assert.Contains("Your shelf is empty", cut.Markup);
    }

    [Fact]
    public async Task Lists_books_sorted_by_title_and_hides_deleted()
    {
        await Store.PutBookAsync(TestBooks.Make("1", "The Silmarillion"));
        await Store.PutBookAsync(TestBooks.Make("2", "Emma"));
        await Store.PutBookAsync(TestBooks.Make("3", "Gone", deleted: true));

        var cut = Render<ShelfPage>();

        var titles = cut.FindAll(".book-row-title").Select(e => e.TextContent.Trim());
        Assert.Equal(["Emma", "The Silmarillion"], titles);
        Assert.Equal("2 volumes", cut.Find("[data-testid=book-count]").TextContent.Trim());
    }

    [Fact]
    public async Task Search_filters_the_list()
    {
        await Store.PutBookAsync(TestBooks.Make("1", "The Hobbit", "Tolkien"));
        await Store.PutBookAsync(TestBooks.Make("2", "Emma", "Austen"));
        var cut = Render<ShelfPage>();

        cut.Find("input[data-testid=search]").Input("austen");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(["Emma"], cut.FindAll(".book-row-title").Select(e => e.TextContent.Trim()));
            Assert.Equal("1 of 2 volumes", cut.Find("[data-testid=book-count]").TextContent.Trim());
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task New_book_appears_without_reload()
    {
        var cut = Render<ShelfPage>();

        await Services.GetRequiredService<BookService>().SaveAsync(new() { Id = "n", Title = "Persuasion" });

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".book-row-title")));
    }

    [Fact]
    public async Task Rows_show_my_stars_and_status_but_not_other_peoples()
    {
        await SignInAsync();
        await Store.PutBookAsync(TestBooks.Make("1", "Emma"));
        await Store.PutBookAsync(TestBooks.Make("2", "Persuasion"));
        await Store.PutReadingAsync(TestReadings.Make("1", "chris", rating: 4, status: Bookshelf.Models.ReadingStatus.Read));
        await Store.PutReadingAsync(TestReadings.Make("2", "wife", rating: 1));

        var cut = Render<ShelfPage>();

        var rows = cut.FindAll(".book-row");
        Assert.Equal("4 out of 5 stars", rows[0].QuerySelector("[data-testid=row-rating]")!.GetAttribute("aria-label"));
        Assert.Equal("Read", rows[0].QuerySelector("[data-testid=row-status]")!.TextContent.Trim());
        Assert.Null(rows[1].QuerySelector("[data-testid=row-rating]"));
    }

    [Fact]
    public async Task Books_being_read_are_pinned_at_the_top()
    {
        await SignInAsync();
        await Store.PutBookAsync(TestBooks.Make("1", "Emma"));
        await Store.PutBookAsync(TestBooks.Make("2", "Persuasion"));
        await Store.PutReadingAsync(TestReadings.Make("2", "chris", status: Bookshelf.Models.ReadingStatus.Reading));
        await Store.PutReadingAsync(TestReadings.Make("1", "wife", status: Bookshelf.Models.ReadingStatus.Reading));

        var cut = Render<ShelfPage>();

        Assert.Equal(["Persuasion"], cut.FindAll("[data-testid=reading-now] .book-row-title").Select(e => e.TextContent.Trim()));
        Assert.Equal(["Emma"], cut.FindAll("[data-testid=book-list] .book-row-title").Select(e => e.TextContent.Trim()));
    }

    [Fact]
    public async Task Filter_chips_show_only_matching_books()
    {
        await SignInAsync();
        await Store.PutBookAsync(TestBooks.Make("1", "Emma"));
        await Store.PutBookAsync(TestBooks.Make("2", "Persuasion"));
        await Store.PutBookAsync(TestBooks.Make("3", "Sanditon"));
        await Store.PutReadingAsync(TestReadings.Make("1", "chris", reads: new Bookshelf.Models.ReadDate(2015)));
        await Store.PutReadingAsync(TestReadings.Make("2", "chris", status: Bookshelf.Models.ReadingStatus.WantToRead));
        var cut = Render<ShelfPage>();
        string[] Titles() => [.. cut.FindAll(".book-row-title").Select(e => e.TextContent.Trim())];

        cut.Find("[data-testid=filter-Read]").Click();
        Assert.Equal(["Emma"], Titles());
        Assert.Equal("1 of 3 volumes", cut.Find("[data-testid=book-count]").TextContent.Trim());

        cut.Find("[data-testid=filter-WantToRead]").Click();
        Assert.Equal(["Persuasion"], Titles());

        cut.Find("[data-testid=filter-NotRead]").Click();
        Assert.Equal(["Persuasion", "Sanditon"], Titles());

        cut.Find("[data-testid=filter-Reading]").Click();
        Assert.Contains("No books marked", cut.Find("[data-testid=no-matches]").TextContent);

        cut.Find("[data-testid=filter-All]").Click();
        Assert.Equal(3, Titles().Length);
    }

    [Fact]
    public async Task Rating_a_book_updates_the_shelf()
    {
        await SignInAsync();
        await Store.PutBookAsync(TestBooks.Make("1", "Emma"));
        var cut = Render<ShelfPage>();

        await Services.GetRequiredService<Bookshelf.Features.ReadingLog.ReadingService>().SetRatingAsync("1", 5);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=row-rating]")));
    }
}

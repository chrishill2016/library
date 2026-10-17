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
}

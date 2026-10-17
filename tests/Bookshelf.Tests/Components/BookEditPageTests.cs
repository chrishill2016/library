using Bookshelf.Features.Books;
using Bookshelf.Tests.Fakes;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Tests.Components;

public class BookEditPageTests : ComponentTestBase
{
    [Fact]
    public void Saving_new_book_stores_it_and_navigates_to_details()
    {
        var cut = Render<BookEditPage>();

        cut.Find("input[data-testid=title]").Input("Middlemarch");
        cut.Find("input[data-testid=author]").Change("George Eliot");
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.Single(Store.Books));
        var book = Store.Books.Values.Single();
        Assert.Equal("Middlemarch", book.Title);
        Assert.Equal("George Eliot", book.Author);
        Assert.EndsWith($"books/{book.Id}", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Title_is_required()
    {
        var cut = Render<BookEditPage>();

        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.Contains("Every book needs a title.", cut.Markup));
        Assert.Empty(Store.Books);
    }

    [Fact]
    public void Back_and_cancel_are_real_links_to_the_shelf()
    {
        var cut = Render<BookEditPage>();

        var links = cut.FindAll("a").Where(a => a.TextContent.Trim() is "Back" or "Cancel").ToList();

        Assert.Equal(2, links.Count);
        Assert.All(links, a => Assert.Equal("./", a.GetAttribute("href")));
    }

    [Fact]
    public async Task Back_and_cancel_return_to_the_book_when_editing()
    {
        await Store.PutBookAsync(TestBooks.Make("e1", "Emma"));

        var cut = Render<BookEditPage>(p => p.Add(x => x.Id, "e1"));

        var links = cut.FindAll("a").Where(a => a.TextContent.Trim() is "Back" or "Cancel").ToList();
        Assert.Equal(2, links.Count);
        Assert.All(links, a => Assert.Equal("books/e1", a.GetAttribute("href")));
    }

    [Fact]
    public void Leaving_the_empty_title_does_not_show_the_error()
    {
        var cut = Render<BookEditPage>();

        // What happens when tapping Back or Cancel straight after opening the page.
        cut.Find("input[data-testid=title]").Blur();

        Assert.DoesNotContain("Every book needs a title.", cut.Markup);
    }

    [Fact]
    public void Title_error_clears_once_a_title_is_typed()
    {
        var cut = Render<BookEditPage>();
        cut.Find("[data-testid=save]").Click();
        cut.WaitForAssertion(() => Assert.Contains("Every book needs a title.", cut.Markup));

        cut.Find("input[data-testid=title]").Input("Emma");

        cut.WaitForAssertion(() => Assert.DoesNotContain("Every book needs a title.", cut.Markup));
    }

    [Fact]
    public async Task Editing_loads_existing_values()
    {
        await Store.PutBookAsync(TestBooks.Make("e1", "Emma", "Jane Austen"));

        var cut = Render<BookEditPage>(p => p.Add(x => x.Id, "e1"));

        Assert.Equal("Emma", cut.Find("input[data-testid=title]").GetAttribute("value"));
    }

    [Fact]
    public void Invalid_isbn_shows_a_warning_but_does_not_block()
    {
        var form = new BookForm { Title = "Old book", Isbn = "12345" };

        Assert.NotNull(form.IsbnWarning);
        Assert.Null(new BookForm { Isbn = "9780261102217" }.IsbnWarning);
        Assert.Equal("12345", form.ToBook().Isbn);
    }
}

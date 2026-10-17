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

        cut.Find("input[data-testid=title]").Change("Middlemarch");
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

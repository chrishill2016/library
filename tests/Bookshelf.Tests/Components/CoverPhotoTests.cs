using Bookshelf.Features.Books;
using Bookshelf.Features.Books.Components;
using Bookshelf.Models;
using Bookshelf.Tests.Fakes;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Tests.Components;

public class CoverPhotoTests : ComponentTestBase
{
    [Fact]
    public void Own_photo_is_shown_instead_of_online_cover()
    {
        Photos.Local["myphoto12345"] = new FakePhotoStore.Photo(true, true, false);
        var book = TestBooks.Make("b1", "Emma") with { CoverUrl = "https://covers/emma.jpg", PhotoId = "myphoto12345" };

        var cut = Render<BookCover>(p => p.Add(x => x.Book, book));

        cut.WaitForAssertion(() => Assert.Equal("blob:myphoto12345", cut.Find("[data-testid=cover-photo]").GetAttribute("src")));
    }

    [Fact]
    public void Falls_back_to_online_cover_when_photo_unavailable()
    {
        var book = TestBooks.Make("b1", "Emma") with { CoverUrl = "https://covers/emma.jpg", PhotoId = "notyetuploaded" };

        var cut = Render<BookCover>(p => p.Add(x => x.Book, book));

        Assert.Equal("https://covers/emma.jpg", cut.Find("[data-testid=cover-image]").GetAttribute("src"));
    }

    [Fact]
    public async Task Details_page_takes_replaces_and_removes_a_photo()
    {
        Services.GetRequiredService<Bookshelf.Services.Firebase.RemoteSession>().SetLibrary("lib1");
        await Store.PutBookAsync(TestBooks.Make("b1", "Emma"));
        var cut = Render<BookDetailsPage>(p => p.Add(x => x.Id, "b1"));

        cut.WaitForElement("input[data-testid=photo-input]").Change("");
        cut.WaitForAssertion(() => Assert.NotNull(Store.Books["b1"].PhotoId));
        var first = Store.Books["b1"].PhotoId!;
        cut.WaitForAssertion(() => Assert.Contains("Replace photo", cut.Find("[data-testid=photo-button]").TextContent));
        Assert.Equal([first], Photos.Remote);

        cut.Find("input[data-testid=photo-input]").Change("");
        cut.WaitForAssertion(() => Assert.NotEqual(first, Store.Books["b1"].PhotoId));
        var second = Store.Books["b1"].PhotoId!;
        Assert.Equal([second], Photos.Remote);

        cut.Find("[data-testid=remove-photo]").Click();
        cut.WaitForAssertion(() => Assert.Null(Store.Books["b1"].PhotoId));
        Assert.Empty(Photos.Remote);
    }

    [Fact]
    public void Cancelling_the_picker_changes_nothing()
    {
        Photos.NextCaptureCancelled = true;
        var cut = Render<BookEditPage>();

        cut.Find("input[data-testid=photo-input]").Change("");

        Assert.Empty(Photos.Local);
        Assert.Contains("Take cover photo", cut.Find("[data-testid=photo-button]").TextContent);
    }

    [Fact]
    public void Photo_taken_while_adding_a_book_is_saved_with_it()
    {
        var cut = Render<BookEditPage>();

        cut.Find("input[data-testid=photo-input]").Change("");
        cut.WaitForAssertion(() => Assert.Contains("Replace photo", cut.Find("[data-testid=photo-button]").TextContent));
        cut.Find("input[data-testid=title]").Input("Emma");
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.Single(Store.Books));
        var book = Store.Books.Values.Single();
        Assert.NotNull(book.PhotoId);
        Assert.True(Photos.Local[book.PhotoId!].Attached);
    }
}

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

        cut.WaitForElement("input[data-testid=photo-input]");
        TakePhoto(cut);
        cut.WaitForAssertion(() => Assert.NotNull(Store.Books["b1"].PhotoId));
        var first = Store.Books["b1"].PhotoId!;
        cut.WaitForAssertion(() => Assert.Contains("Replace photo", cut.Find("[data-testid=photo-button]").TextContent));
        Assert.Equal([first], Photos.Remote);

        TakePhoto(cut);
        cut.WaitForAssertion(() => Assert.NotEqual(first, Store.Books["b1"].PhotoId));
        var second = Store.Books["b1"].PhotoId!;
        Assert.Equal([second], Photos.Remote);

        cut.Find("[data-testid=remove-photo]").Click();
        cut.WaitForAssertion(() => Assert.Null(Store.Books["b1"].PhotoId));
        Assert.Empty(Photos.Remote);
    }

    [Fact]
    public void Photo_button_opens_the_camera_and_gallery_button_picks_a_picture()
    {
        var cut = Render<BookEditPage>();

        Assert.Equal("environment", cut.Find("input[data-testid=photo-input]").GetAttribute("capture"));
        Assert.False(cut.Find("input[data-testid=photo-gallery-input]").HasAttribute("capture"));

        TakePhoto(cut, "photo-gallery-input");
        cut.WaitForAssertion(() => Assert.Contains("Replace photo", cut.Find("[data-testid=photo-button]").TextContent));
        Assert.Single(Photos.Local);
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

        TakePhoto(cut);
        cut.WaitForAssertion(() => Assert.Contains("Replace photo", cut.Find("[data-testid=photo-button]").TextContent));
        cut.Find("input[data-testid=title]").Input("Emma");
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.Single(Store.Books));
        var book = Store.Books.Values.Single();
        Assert.NotNull(book.PhotoId);
        Assert.True(Photos.Local[book.PhotoId!].Attached);
    }

    [Fact]
    public void Taking_a_photo_opens_the_cropper_on_the_guessed_outline()
    {
        var cut = Render<BookEditPage>();

        cut.Find("input[data-testid=photo-input]").Change("");

        cut.WaitForElement("[data-testid=cropper]");
        var draft = Assert.Single(Photos.Drafts.Keys);
        Assert.Equal([draft], Photos.CroppersShown);
        Assert.Contains("Drag the corners", cut.Find("[data-testid=cropper]").TextContent);
        Assert.Empty(Photos.Local);
    }

    [Fact]
    public void Cropper_says_when_it_could_not_find_the_book()
    {
        Photos.NextDraftFound = false;
        var cut = Render<BookEditPage>();

        cut.Find("input[data-testid=photo-input]").Change("");

        Assert.Contains("Couldn't spot the book's edges", cut.WaitForElement("[data-testid=cropper]").TextContent);
    }

    [Fact]
    public void Cancelling_the_crop_discards_the_photo()
    {
        var cut = Render<BookEditPage>();
        cut.Find("input[data-testid=photo-input]").Change("");

        cut.WaitForElement("[data-testid=crop-cancel]").Click();

        Assert.Empty(cut.FindAll("[data-testid=cropper]"));
        Assert.Empty(Photos.Drafts);
        Assert.Empty(Photos.Local);
        Assert.Contains("Take cover photo", cut.Find("[data-testid=photo-button]").TextContent);
    }

    [Fact]
    public void Whole_photo_toggles_between_the_full_frame_and_the_guess()
    {
        var cut = Render<BookEditPage>();
        cut.Find("input[data-testid=photo-input]").Change("");
        var draft = Assert.Single(Photos.Drafts.Keys);

        cut.WaitForElement("[data-testid=crop-whole]").Click();
        Assert.True(Photos.Drafts[draft]);
        Assert.Contains("Guess again", cut.Find("[data-testid=crop-whole]").TextContent);

        cut.Find("[data-testid=crop-whole]").Click();
        Assert.False(Photos.Drafts[draft]);
    }

    /// <summary>Chooses a photo, then accepts the crop.</summary>
    private static void TakePhoto(IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> cut, string input = "photo-input")
    {
        cut.Find($"input[data-testid={input}]").Change("");
        cut.WaitForElement("[data-testid=crop-use]").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=cropper]")));
    }
}

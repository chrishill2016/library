using Bookshelf.Features.Lookup;
using Bookshelf.Features.Scan;
using Bookshelf.Models;
using Bunit;

namespace Bookshelf.Tests.Components;

public class ScanPageTests : ComponentTestBase
{
    private const string Hobbit = "9780261102217";
    private const string Emma = "9780141439587";

    public ScanPageTests() => Lookup.Add(Hobbit, "The Hobbit", "J. R. R. Tolkien");

    private IRenderedComponent<ScanPage> RenderWithCamera()
    {
        Scanner.Supported = true;
        var cut = Render<ScanPage>();
        cut.WaitForAssertion(() => Assert.True(Scanner.Started));
        return cut;
    }

    private static string[] Statuses(IRenderedComponent<ScanPage> cut) =>
        [.. cut.FindAll("[data-testid=scan-status]").Select(e => e.TextContent.Trim())];

    [Fact]
    public void Starts_the_camera_when_supported()
    {
        RenderWithCamera();
    }

    [Fact]
    public void Explains_and_offers_typing_when_camera_scanning_unsupported()
    {
        var cut = Render<ScanPage>();

        Assert.NotNull(cut.Find("[data-testid=scan-unsupported]"));
        Assert.NotNull(cut.Find("input[data-testid=isbn-entry]"));
        Assert.False(Scanner.Started);
    }

    [Fact]
    public async Task Rapid_scan_adds_books_and_counts_them()
    {
        var cut = RenderWithCamera();

        await cut.InvokeAsync(() => Scanner.ScanAsync(Hobbit));
        await cut.InvokeAsync(() => Scanner.ScanAsync(Emma));

        cut.WaitForAssertion(() => Assert.Equal("2 added", cut.Find("[data-testid=added-count]").TextContent.Trim()));
        Assert.Contains("Saved by ISBN. Not found online", Statuses(cut)[0]);
        Assert.Equal("Added to the shelf", Statuses(cut)[1]);
        Assert.Equal(2, Store.Books.Count);
    }

    [Fact]
    public async Task Non_book_barcodes_are_ignored()
    {
        var cut = RenderWithCamera();

        await cut.InvokeAsync(() => Scanner.ScanAsync("5000000000005"));

        Assert.Empty(cut.FindAll("[data-testid=scan-item]"));
    }

    [Fact]
    public async Task Undo_removes_a_scanned_book()
    {
        var cut = RenderWithCamera();
        await cut.InvokeAsync(() => Scanner.ScanAsync(Hobbit));

        cut.WaitForElement("[data-testid=scan-undo]").Click();

        cut.WaitForAssertion(() => Assert.Equal("Removed", Statuses(cut)[0]));
        Assert.True(Store.Books.Values.Single().IsDeleted);
    }

    [Fact]
    public async Task Confirm_mode_waits_for_add_and_remembers_the_setting()
    {
        var cut = RenderWithCamera();
        cut.Find("input[data-testid=confirm-toggle]").Change(true);
        cut.WaitForAssertion(() => Assert.Equal("true", Store.Settings[ScanPage.ConfirmEachKey]));

        await cut.InvokeAsync(() => Scanner.ScanAsync(Hobbit));
        cut.WaitForElement("[data-testid=scan-add]");
        Assert.Empty(Store.Books);

        cut.Find("[data-testid=scan-add]").Click();

        cut.WaitForAssertion(() => Assert.Equal("The Hobbit", Store.Books.Values.Single().Title));
    }

    [Fact]
    public async Task Duplicate_scan_warns_and_offers_another_copy()
    {
        await Services.GetService<Bookshelf.Features.Books.BookService>()!
            .SaveAsync(new Book { Id = "existing", Title = "The Hobbit", Isbn = "0261102214" });
        var cut = RenderWithCamera();

        await cut.InvokeAsync(() => Scanner.ScanAsync(Hobbit));

        cut.WaitForAssertion(() => Assert.Equal("Already on the shelf", Statuses(cut)[0]));
        Assert.Single(Store.Books);

        cut.Find("[data-testid=scan-add-copy]").Click();
        cut.WaitForAssertion(() => Assert.Equal(2, Store.Books.Count));
    }

    [Fact]
    public void Typed_isbn_is_looked_up_like_a_scan()
    {
        var cut = Render<ScanPage>();

        cut.Find("input[data-testid=isbn-entry]").Change("0-261-10221-4");
        cut.Find("[data-testid=isbn-submit]").Click();

        cut.WaitForAssertion(() => Assert.Equal("The Hobbit", Store.Books.Values.Single().Title));
    }

    [Fact]
    public void Offline_scan_is_saved_for_later()
    {
        Connectivity.Set(false);
        var cut = Render<ScanPage>();

        cut.Find("input[data-testid=isbn-entry]").Change(Hobbit);
        cut.Find("[data-testid=isbn-submit]").Click();

        cut.WaitForAssertion(() => Assert.Equal(BookLookupState.Pending, Store.Books.Values.Single().LookupState));
        Assert.Contains("filled in when you're online", Statuses(cut)[0]);
    }
}

file static class ServiceProviderExtensions
{
    public static T? GetService<T>(this IServiceProvider services) => (T?)services.GetService(typeof(T));
}

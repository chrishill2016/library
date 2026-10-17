using Bookshelf.Features.Books;
using Bookshelf.Features.Lookup;
using Bookshelf.Features.ReadingLog;
using Bookshelf.Models;
using Bookshelf.Tests.Fakes;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Tests.Components;

public class ReadingListPageTests : ComponentTestBase
{
    private ReadingService Readings => Services.GetRequiredService<ReadingService>();

    private async Task QueueAsync(params string[] titles)
    {
        foreach (var title in titles)
        {
            await Store.PutBookAsync(TestBooks.Make(title.ToLowerInvariant(), title));
            await Readings.SetStatusAsync(title.ToLowerInvariant(), ReadingStatus.WantToRead);
        }
    }

    private static string[] Titles(IRenderedComponent<ReadingListPage> cut) =>
        [.. cut.FindAll("[data-testid=queue-row] .book-row-title").Select(e => e.TextContent.Trim())];

    [Fact]
    public async Task Empty_list_explains_how_to_add()
    {
        await SignInAsync();

        var cut = Render<ReadingListPage>();

        Assert.Contains("Want to read", cut.Find("[data-testid=queue-empty]").TextContent);
    }

    [Fact]
    public async Task Lists_my_queue_in_order_and_menu_moves_books()
    {
        await SignInAsync();
        await QueueAsync("Emma", "Persuasion", "Sanditon");
        var cut = Render<ReadingListPage>();
        Assert.Equal(["Emma", "Persuasion", "Sanditon"], Titles(cut));
        Assert.Equal("3 books", cut.Find("[data-testid=queue-count]").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Instance.OnReorder(2, 0));

        cut.WaitForAssertion(() => Assert.Equal(["Sanditon", "Emma", "Persuasion"], Titles(cut)));
    }

    [Fact]
    public async Task Start_reading_takes_the_top_book_off_the_list()
    {
        await SignInAsync();
        await QueueAsync("Emma", "Persuasion");
        var cut = Render<ReadingListPage>();

        cut.Find("[data-testid=start-reading]").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Persuasion"], Titles(cut)));
        Assert.Equal(ReadingStatus.Reading, Store.Readings["emma_chris"].Status);
    }

    [Fact]
    public async Task Shows_the_other_persons_list_read_only()
    {
        await SignInAsync();
        await QueueAsync("Emma");
        await Store.PutBookAsync(TestBooks.Make("p", "Persuasion") with { OnWishlist = true });
        await Store.PutReadingAsync(TestReadings.Make("p", "wife", status: ReadingStatus.WantToRead, name: "Sarah") with { QueueOrder = 1 });
        Readings.Invalidate();
        var cut = Render<ReadingListPage>();

        var tab = cut.Find("[data-testid=list-other]");
        Assert.Contains("Sarah", tab.TextContent);
        tab.Click();

        cut.WaitForAssertion(() => Assert.Equal(["Persuasion"], Titles(cut)));
        Assert.NotNull(cut.Find("[data-testid=wishlist-tag]"));
        Assert.Empty(cut.FindAll("[data-testid=start-reading]"));
        Assert.Empty(cut.FindAll("[data-drag-handle]"));
    }

    [Fact]
    public async Task Wishlist_add_from_find_online_queues_the_book_and_keeps_it_off_the_shelf()
    {
        await SignInAsync();
        Lookup.SearchResults.Add(new BookCandidate("Middlemarch", "George Eliot", "9780141439549", null, 1871, null, "test"));
        var find = Render<FindOnlinePage>();
        find.Find("input[data-testid=find-query]").Input("middlemarch");
        find.Find("[data-testid=find-submit]").Click();

        find.WaitForElement("[data-testid=find-wishlist]").Click();

        find.WaitForAssertion(() => Assert.Single(Store.Books));
        var book = Store.Books.Values.Single();
        Assert.True(book.OnWishlist);
        Assert.Equal(ReadingStatus.WantToRead, Store.Readings[Reading.IdFor(book.Id, "chris")].Status);
        var shelf = Render<ShelfPage>();
        Assert.Empty(shelf.FindAll("[data-testid=book-list] .book-row"));
        shelf.Find("[data-testid=filter-Wishlist]").Click();
        Assert.Single(shelf.FindAll(".book-row"));
    }

    [Fact]
    public async Task Got_it_moves_a_wishlist_book_onto_the_shelf()
    {
        await SignInAsync();
        await Store.PutBookAsync(TestBooks.Make("w", "Middlemarch") with { OnWishlist = true });
        var cut = Render<BookDetailsPage>(p => p.Add(x => x.Id, "w"));

        cut.WaitForElement("[data-testid=move-to-shelf]").Click();

        cut.WaitForAssertion(() => Assert.False(Store.Books["w"].OnWishlist));
        Assert.Empty(cut.FindAll("[data-testid=wishlist-banner]"));
    }

    [Fact]
    public async Task Book_page_shows_its_place_on_my_list()
    {
        await SignInAsync();
        await QueueAsync("Emma", "Persuasion");
        var cut = Render<BookDetailsPage>(p => p.Add(x => x.Id, "persuasion"));

        Assert.Contains("Number 2", cut.WaitForElement("[data-testid=queue-position]").TextContent);
    }

    [Fact]
    public async Task Typed_in_wishlist_book_goes_on_my_list_not_the_shelf()
    {
        await SignInAsync();
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo("books/new?wishlist=true");
        var cut = Render<BookEditPage>();

        cut.Find("input[data-testid=title]").Input("Middlemarch");
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.Single(Store.Books));
        var book = Store.Books.Values.Single();
        Assert.True(book.OnWishlist);
        Assert.True(Store.Readings[Reading.IdFor(book.Id, "chris")].IsQueued);
    }
}

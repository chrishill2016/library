using Bookshelf.Features.Books;
using Bookshelf.Models;
using Bookshelf.Tests.Fakes;

namespace Bookshelf.Tests.Books;

public class BookSearchTests
{
    private static readonly Book[] Shelf =
    [
        TestBooks.Make("1", "The Hobbit", "J. R. R. Tolkien") with { Isbn = "978-0-261-10221-7" },
        TestBooks.Make("2", "Les Misérables", "Victor Hugo"),
        TestBooks.Make("3", "A Tale of Two Cities", "Charles Dickens"),
        TestBooks.Make("4", "Good Omens", "Neil Gaiman & Terry Pratchett"),
    ];

    [Theory]
    [InlineData("hobbit", "1")]
    [InlineData("TOLKIEN", "1")]
    [InlineData("miserables", "2")]
    [InlineData("dickens tale", "3")]
    [InlineData("pratchett", "4")]
    [InlineData("9780261102217", "1")]
    [InlineData("0-261-10221", "1")]
    public void Filter_matches_title_author_isbn_ignoring_case_accents_and_hyphens(string query, string expectedId)
    {
        var result = BookSearch.Filter(Shelf, query).Select(b => b.Id);

        Assert.Equal([expectedId], result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_query_returns_everything(string? query) =>
        Assert.Equal(4, BookSearch.Filter(Shelf, query).Count());

    [Fact]
    public void All_terms_must_match() =>
        Assert.Empty(BookSearch.Filter(Shelf, "hobbit dickens"));

    [Fact]
    public void Title_sort_ignores_leading_articles()
    {
        var titles = BookSearch.Sort(Shelf, BookSort.Title).Select(b => b.Title);

        Assert.Equal(["Good Omens", "The Hobbit", "Les Misérables", "A Tale of Two Cities"], titles);
    }

    [Fact]
    public void Author_sort_uses_surname_of_first_author()
    {
        var ids = BookSearch.Sort(Shelf, BookSort.Author).Select(b => b.Id);

        Assert.Equal(["3", "4", "2", "1"], ids);
    }

    [Fact]
    public void Recently_added_sort_is_newest_first()
    {
        var books = new[]
        {
            TestBooks.Make("old") with { AddedUtc = TestBooks.Epoch },
            TestBooks.Make("new") with { AddedUtc = TestBooks.Epoch.AddDays(1) },
        };

        Assert.Equal(["new", "old"], BookSearch.Sort(books, BookSort.RecentlyAdded).Select(b => b.Id));
    }

    [Theory]
    [InlineData("J. R. R. Tolkien", "Tolkien J. R. R.")]
    [InlineData("Tolkien, J. R. R.", "Tolkien, J. R. R.")]
    [InlineData("Neil Gaiman & Terry Pratchett", "Gaiman Neil")]
    [InlineData("Neil Gaiman, Terry Pratchett", "Gaiman Neil")]
    [InlineData("Homer", "Homer")]
    [InlineData("", "")]
    public void Author_sort_key(string author, string expected) =>
        Assert.Equal(expected, BookSearch.AuthorSortKey(author));

    [Theory]
    [InlineData("The Hobbit", "Hobbit")]
    [InlineData("An Instance of the Fingerpost", "Instance of the Fingerpost")]
    [InlineData("Theory of Everything", "Theory of Everything")]
    [InlineData("The", "The")]
    public void Title_sort_key(string title, string expected) =>
        Assert.Equal(expected, BookSearch.TitleSortKey(title));

    [Fact]
    public void Sorts_by_my_rating_highest_first_with_unrated_last()
    {
        Book[] books = [TestBooks.Make("1", "Unrated"), TestBooks.Make("2", "Three"), TestBooks.Make("3", "Five"), TestBooks.Make("4", "Also three")];
        var ratings = new Dictionary<string, int> { ["2"] = 3, ["3"] = 5, ["4"] = 3 };

        var sorted = BookSearch.Sort(books, BookSort.MyRating,
            b => ratings.TryGetValue(b.Id, out var r) ? TestReadings.Make(b.Id, "me", rating: r) : null);

        Assert.Equal(["Five", "Also three", "Three", "Unrated"], sorted.Select(b => b.Title));
    }

    [Fact]
    public void Read_filter_counts_read_dates_or_read_status()
    {
        Book[] books = [TestBooks.Make("1"), TestBooks.Make("2"), TestBooks.Make("3"), TestBooks.Make("4")];
        var mine = new Dictionary<string, Reading>
        {
            ["1"] = TestReadings.Make("1", "me", status: ReadingStatus.Read),
            ["2"] = TestReadings.Make("2", "me", reads: new ReadDate(2001)),
            ["3"] = TestReadings.Make("3", "me", status: ReadingStatus.Reading),
        };
        Reading? Mine(Book b) => mine.GetValueOrDefault(b.Id);

        Assert.Equal(["1", "2"], BookSearch.FilterByReading(books, ReadingFilter.Read, Mine).Select(b => b.Id));
        Assert.Equal(["3", "4"], BookSearch.FilterByReading(books, ReadingFilter.NotRead, Mine).Select(b => b.Id));
        Assert.Equal(["3"], BookSearch.FilterByReading(books, ReadingFilter.Reading, Mine).Select(b => b.Id));
        Assert.Equal(4, BookSearch.FilterByReading(books, ReadingFilter.All, Mine).Count());
    }

    [Fact]
    public void Wishlist_books_only_show_under_the_wishlist_filter()
    {
        Book[] books = [TestBooks.Make("owned"), TestBooks.Make("wanted") with { OnWishlist = true }];

        Assert.Equal(["owned"], BookSearch.FilterByReading(books, ReadingFilter.All, _ => null).Select(b => b.Id));
        Assert.Equal(["owned"], BookSearch.FilterByReading(books, ReadingFilter.NotRead, _ => null).Select(b => b.Id));
        Assert.Equal(["wanted"], BookSearch.FilterByReading(books, ReadingFilter.Wishlist, _ => null).Select(b => b.Id));
    }
}

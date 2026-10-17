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
}

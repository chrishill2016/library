using Bookshelf.Features.Lookup;
using Bookshelf.Models;
using Bunit;

namespace Bookshelf.Tests.Components;

public class FindOnlinePageTests : ComponentTestBase
{
    private IRenderedComponent<FindOnlinePage> Search(string query)
    {
        var cut = Render<FindOnlinePage>();
        cut.Find("input[data-testid=find-query]").Input(query);
        cut.Find("[data-testid=find-submit]").Click();
        return cut;
    }

    [Fact]
    public void Shows_results_and_adds_one()
    {
        Lookup.SearchResults.Add(new BookCandidate("Emma", "Jane Austen", "9780141439587", "Penguin", 2003, null, "Fake"));
        Lookup.SearchResults.Add(new BookCandidate("Emma and the Vampires", "Wayne Josephson", null, null, 2010, null, "Fake"));

        var cut = Search("emma");

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid=find-result]").Count));
        cut.FindAll("[data-testid=find-add]")[0].Click();

        cut.WaitForAssertion(() => Assert.Equal("Emma", Store.Books.Values.Single().Title));
        Assert.Contains("Added", cut.FindAll("[data-testid=find-result]")[0].TextContent);
    }

    [Fact]
    public async Task Books_already_on_the_shelf_are_marked()
    {
        await Store.PutBookAsync(new Book { Id = "e", Title = "Emma", Isbn = "0141439580" });
        Lookup.SearchResults.Add(new BookCandidate("Emma", "Jane Austen", "9780141439587", null, null, null, "Fake"));

        var cut = Search("emma");

        cut.WaitForAssertion(() => Assert.Contains("Already on the shelf", cut.Find("[data-testid=find-result]").TextContent));
        Assert.Empty(cut.FindAll("[data-testid=find-add]"));
    }

    [Fact]
    public void Says_when_nothing_is_found()
    {
        var cut = Search("zzzz");

        cut.WaitForAssertion(() => Assert.Contains("Nothing found for “zzzz”", cut.Find("[data-testid=find-empty]").TextContent));
    }
}

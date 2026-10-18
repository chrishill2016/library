using Bookshelf.Features.Genres;

namespace Bookshelf.Tests.Genres;

public class GenreCatalogueTests
{
    [Theory]
    [InlineData("Fiction / Mystery & Detective / General", "crime")]
    [InlineData("Fiction / Fantasy / Epic", "fantasy")]
    [InlineData("Fiction / Science Fiction / General", "scifi")]
    [InlineData("Fiction / Historical / General", "historical-fiction")]
    [InlineData("Fiction / Romance / Contemporary", "romance")]
    [InlineData("Fiction / Classics", "classics")]
    [InlineData("Fiction / Literary", "literary")]
    [InlineData("Fiction", "literary")]
    [InlineData("Biography & Autobiography / Personal Memoirs", "biography")]
    [InlineData("History / Europe / Great Britain", "history")]
    [InlineData("Cooking / Regional & Ethnic / British", "food")]
    [InlineData("Gardening / General", "home-garden")]
    [InlineData("Juvenile Fiction / Animals / Bears", "children")]
    [InlineData("Young Adult Fiction / Dystopian", "young-adult")]
    [InlineData("Comics & Graphic Novels / Manga", "graphic")]
    [InlineData("Travel / Europe / Italy", "travel")]
    [InlineData("Religion / Christianity", "religion")]
    [InlineData("Reference / Dictionaries", "reference")]
    [InlineData("Poetry / European / English", "poetry")]
    public void Maps_google_categories(string category, string expected) =>
        Assert.Equal(expected, GenreCatalogue.Suggest([category])[0]);

    [Fact]
    public void Science_fiction_is_not_science_and_historical_fiction_is_not_history()
    {
        Assert.Equal(["scifi"], GenreCatalogue.Suggest(["Fiction / Science Fiction / Space Opera"]));
        Assert.DoesNotContain("history", GenreCatalogue.Suggest(["Fiction / Historical / General"]));
    }

    [Fact]
    public void Open_library_subjects_are_matched_but_count_for_less()
    {
        var genres = GenreCatalogue.Suggest(
            ["Fiction / Fantasy / General"],
            ["Fantasy fiction", "Dragons", "Juvenile fiction", "Wizards", "Middle Earth (Imaginary place)"]);

        Assert.Equal("fantasy", genres[0]);
        Assert.Contains("children", genres);
        Assert.True(genres.Count <= GenreCatalogue.MaxPerBook);
    }

    [Fact]
    public void Words_match_whole_words_only()
    {
        // "art" must not match "party" or "heart"; "pets" not "trumpets".
        Assert.Empty(GenreCatalogue.Suggest(null, ["Party politics", "Heartbreak", "Trumpets"]));
        Assert.Equal(["art"], GenreCatalogue.Suggest(["Art / History"]).Take(1));
    }

    [Fact]
    public void Nothing_recognisable_suggests_nothing() =>
        Assert.Empty(GenreCatalogue.Suggest([], ["Accessible book", "Protected DAISY", "In library"]));

    [Fact]
    public void At_most_three_highest_scoring()
    {
        var genres = GenreCatalogue.Suggest(
            ["Fiction / Mystery & Detective", "Fiction / Romance", "Fiction / Humorous", "Fiction / Fantasy"],
            ["Detective and mystery stories", "Romance"]);

        Assert.Equal(3, genres.Count);
        Assert.Equal(["crime", "romance"], genres.Take(2));
    }

    [Fact]
    public void Normalize_keeps_known_keys_in_catalogue_order()
    {
        Assert.Equal(["crime", "history", "food"], GenreCatalogue.Normalize(["children", "nonsense", "crime", "history", "crime", "food"]));
        Assert.Empty(GenreCatalogue.Normalize(null));
    }

    [Fact]
    public void Keys_are_unique_and_have_labels()
    {
        Assert.Equal(GenreCatalogue.All.Count, GenreCatalogue.All.Select(g => g.Key).Distinct().Count());
        Assert.Equal("Crime & Thriller", GenreCatalogue.Label("crime"));
        Assert.Equal("unknown-key", GenreCatalogue.Label("unknown-key"));
    }
}

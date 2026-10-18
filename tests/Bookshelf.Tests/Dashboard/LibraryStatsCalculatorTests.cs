using Bookshelf.Features.Dashboard;
using Bookshelf.Models;
using Bookshelf.Tests.Fakes;

namespace Bookshelf.Tests.Dashboard;

public class LibraryStatsCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 18);

    private static Book B(string id, string title = "", string author = "", int? year = null, bool wishlist = false, DateTimeOffset? added = null, bool deleted = false) =>
        TestBooks.Make(id, title == "" ? id : title, author, deleted: deleted) with
        {
            PublishedYear = year,
            OnWishlist = wishlist,
            AddedUtc = added ?? TestBooks.Epoch,
        };

    private static Reading R(string bookId, int? rating = null, string? status = null, params ReadDate[] reads) =>
        TestReadings.Make(bookId, "me", rating: rating, status: status, reads: reads);

    private static LibraryStats Calc(Book[] books, Reading[] readings, StatsPeriod period = StatsPeriod.CalendarYear) =>
        LibraryStatsCalculator.Calculate(books, readings, period, Today);

    [Fact]
    public void Counts_the_shelf_wishlist_and_books_added_this_year()
    {
        var stats = Calc(
        [
            B("a", added: new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero)),
            B("b", added: new DateTimeOffset(2025, 12, 1, 9, 0, 0, TimeSpan.Zero)),
            B("w", wishlist: true),
            B("gone", deleted: true),
        ], []);

        Assert.Equal(2, stats.OnShelf);
        Assert.Equal(1, stats.OnWishlist);
        Assert.Equal(1, stats.AddedInPeriod);
        Assert.Equal("in 2026", stats.PeriodLabel);
        Assert.False(stats.HasReadingData);
    }

    [Fact]
    public void Calendar_year_and_last_12_months_count_different_reads()
    {
        Book[] books = [B("a"), B("b"), B("c")];
        Reading[] readings =
        [
            R("a", reads: new ReadDate(2026, 2, 3)),
            R("b", reads: new ReadDate(2025, 11, 20)),
            R("c", reads: new ReadDate(2025, 9, 1)),
        ];

        Assert.Equal(1, Calc(books, readings, StatsPeriod.CalendarYear).ReadInPeriod);
        var rolling = Calc(books, readings, StatsPeriod.Last12Months);
        Assert.Equal(2, rolling.ReadInPeriod);
        Assert.Equal("in the last 12 months", rolling.PeriodLabel);
    }

    [Theory]
    [InlineData(2026, null, null, StatsPeriod.CalendarYear, true)]
    [InlineData(2025, null, null, StatsPeriod.CalendarYear, false)]
    [InlineData(2025, null, null, StatsPeriod.Last12Months, true)]    // 2025 overlaps 19 Oct 2025 – 18 Oct 2026
    [InlineData(2025, 10, null, StatsPeriod.Last12Months, true)]      // October 2025 overlaps from the 19th
    [InlineData(2025, 9, null, StatsPeriod.Last12Months, false)]
    [InlineData(2025, 10, 18, StatsPeriod.Last12Months, false)]
    [InlineData(2025, 10, 19, StatsPeriod.Last12Months, true)]
    public void Vague_dates_count_when_their_span_overlaps_the_window(int year, int? month, int? day, StatsPeriod period, bool counts)
    {
        var (from, to) = LibraryStatsCalculator.Window(period, Today);

        Assert.Equal(counts, LibraryStatsCalculator.Overlaps(new ReadDate(year, month, day), from, to));
    }

    [Fact]
    public void Re_reads_count_each_time_but_read_all_time_counts_books()
    {
        var stats = Calc([B("a"), B("b")],
        [
            R("a", reads: [new ReadDate(2026, 1, 5), new ReadDate(2026, 8, 1)]),
            R("b", status: ReadingStatus.Read),
        ]);

        Assert.Equal(2, stats.ReadInPeriod);
        Assert.Equal(2, stats.ReadAllTime);
        Assert.Equal(new ReadDate(2026, 8, 1), stats.LastFinished!.Date);
    }

    [Fact]
    public void Reads_per_year_fills_gaps_and_ends_this_year()
    {
        var stats = Calc([B("a"), B("b"), B("c")],
        [
            R("a", reads: new ReadDate(2022)),
            R("b", reads: new ReadDate(2022, 5)),
            R("c", reads: new ReadDate(2024, 1, 1)),
        ]);

        Assert.Equal(
            [new YearCount(2022, 2), new YearCount(2023, 0), new YearCount(2024, 1), new YearCount(2025, 0), new YearCount(2026, 0)],
            stats.ReadsPerYear);
    }

    [Fact]
    public void Chart_shows_at_most_ten_years()
    {
        var stats = Calc([B("a"), B("b")], [R("a", reads: new ReadDate(1990)), R("b", reads: new ReadDate(2026))]);

        Assert.Equal(10, stats.ReadsPerYear.Count);
        Assert.Equal(2017, stats.ReadsPerYear[0].Year);
    }

    [Fact]
    public void Ratings_average_and_breakdown()
    {
        var stats = Calc([B("a"), B("b"), B("c"), B("d")], [R("a", 5), R("b", 4), R("c", 5), R("d")]);

        Assert.Equal(3, stats.Rated);
        Assert.Equal(4.7, stats.AverageRating);
        Assert.Equal([0, 0, 0, 1, 2], stats.RatingCounts);
        Assert.Equal(["a", "c"], stats.FiveStarBooks.Select(b => b.Id).Order());
    }

    [Fact]
    public void Most_read_authors_split_co_authors_and_need_two_books()
    {
        var read = new ReadDate(2020);
        var stats = Calc(
        [
            B("e", author: "Jane Austen"), B("p", author: "Jane Austen"), B("s", author: "jane austen & Another Writer"),
            B("h", author: "J. R. R. Tolkien"), B("x", author: "Another Writer"), B("u", author: "Unread Author"),
        ],
        [R("e", reads: read), R("p", reads: read), R("s", reads: read), R("h", reads: read), R("x", reads: read)]);

        Assert.Equal([new AuthorCount("Jane Austen", 3), new AuthorCount("Another Writer", 2)], stats.TopAuthors);
    }

    [Fact]
    public void Reading_now_list_unread_next_up_and_oldest()
    {
        var stats = Calc(
            [B("a", year: 1813), B("b", year: 1954), B("c"), B("w", wishlist: true, year: 1700)],
            [
                R("a", status: ReadingStatus.Read),
                R("b", status: ReadingStatus.Reading),
                R("c", status: ReadingStatus.WantToRead) with { QueueOrder = 2 },
                R("w", status: ReadingStatus.WantToRead) with { QueueOrder = 1 },
            ]);

        Assert.Equal(1, stats.ReadingNow);
        Assert.Equal(2, stats.OnReadingList);
        Assert.Equal("w", stats.NextUp!.Id);
        Assert.Equal(2, stats.UnreadOnShelf);
        Assert.Equal("a", stats.OldestBook!.Id);
    }

    [Fact]
    public void Ignores_readings_of_deleted_books()
    {
        var stats = Calc([B("gone", deleted: true)], [R("gone", 5, reads: new ReadDate(2026))]);

        Assert.Equal(0, stats.ReadInPeriod);
        Assert.Equal(0, stats.Rated);
    }

    [Fact]
    public void Year_in_books_lists_that_years_reads_newest_first()
    {
        Book[] books = [B("a", "Emma"), B("b", "Persuasion"), B("c", "Sanditon")];
        Reading[] readings =
        [
            R("a", reads: [new ReadDate(2019), new ReadDate(2024, 6, 1)]),
            R("b", reads: new ReadDate(2024, 2)),
            R("c", reads: new ReadDate(2023, 1, 1)),
        ];

        Assert.Equal(["Emma", "Persuasion"], LibraryStatsCalculator.ReadInYear(books, readings, 2024).Select(r => r.Book.Title));
        Assert.Equal([2024, 2023, 2019], LibraryStatsCalculator.YearsWithReads(readings));
    }
}

using Bookshelf.Models;

namespace Bookshelf.Features.Dashboard;

/// <summary>What "this year" means on the dashboard (a per-device setting).</summary>
public enum StatsPeriod
{
    /// <summary>1 January to today.</summary>
    CalendarYear,

    /// <summary>The 12 months up to today.</summary>
    Last12Months,
}

public sealed record AuthorCount(string Author, int Books);

public sealed record YearCount(int Year, int Books);

public sealed record ReadBook(Book Book, Reading Reading, ReadDate Date);

/// <summary>Headline numbers and lists for the dashboard. Reading stats are the signed-in person's own.</summary>
public sealed record LibraryStats
{
    public required StatsPeriod Period { get; init; }
    public required string PeriodLabel { get; init; }

    // The library
    public int OnShelf { get; init; }
    public int OnWishlist { get; init; }
    public int AddedInPeriod { get; init; }
    public Book? OldestBook { get; init; }

    // My reading
    public int ReadAllTime { get; init; }
    public int ReadInPeriod { get; init; }
    public int ReadingNow { get; init; }
    public int OnReadingList { get; init; }
    public int UnreadOnShelf { get; init; }
    public Book? NextUp { get; init; }
    public ReadBook? LastFinished { get; init; }
    public IReadOnlyList<YearCount> ReadsPerYear { get; init; } = [];

    // Taste
    public double? AverageRating { get; init; }
    public int Rated { get; init; }

    /// <summary>Number of books given 1–5 stars (index 0 = 1 star).</summary>
    public IReadOnlyList<int> RatingCounts { get; init; } = [0, 0, 0, 0, 0];
    public IReadOnlyList<AuthorCount> TopAuthors { get; init; } = [];
    public IReadOnlyList<Book> FiveStarBooks { get; init; } = [];

    public bool HasReadingData => ReadAllTime > 0 || Rated > 0 || ReadingNow > 0 || OnReadingList > 0;
}

/// <summary>Works out <see cref="LibraryStats"/> from the books and my readings. Pure, so it's easy to test.</summary>
public static class LibraryStatsCalculator
{
    private const int MaxChartYears = 10;

    public static LibraryStats Calculate(
        IReadOnlyList<Book> books,
        IReadOnlyList<Reading> myReadings,
        StatsPeriod period,
        DateOnly today)
    {
        var live = books.Where(b => !b.IsDeleted).ToList();
        var byId = live.ToDictionary(b => b.Id);
        var shelf = live.Where(b => !b.OnWishlist).ToList();
        var mine = myReadings.Where(r => byId.ContainsKey(r.BookId)).ToList();
        var mineById = mine.ToDictionary(r => r.BookId);
        var (from, to) = Window(period, today);

        // Each read date is one finish (re-reads count again), on books we still have or want.
        var reads = mine
            .SelectMany(r => r.Reads.Select(d => new ReadBook(byId[r.BookId], r, d)))
            .Where(r => r.Date.IsValid)
            .ToList();

        var rated = mine.Where(r => r.Rating is >= 1 and <= 5).ToList();
        var ratingCounts = Enumerable.Range(1, 5).Select(stars => rated.Count(r => r.Rating == stars)).ToList();

        var queue = mine.Where(r => r.IsQueued).OrderBy(r => r.QueueOrder ?? double.MaxValue).ToList();

        return new LibraryStats
        {
            Period = period,
            PeriodLabel = period == StatsPeriod.CalendarYear ? $"in {today.Year}" : "in the last 12 months",
            OnShelf = shelf.Count,
            OnWishlist = live.Count - shelf.Count,
            AddedInPeriod = shelf.Count(b => InWindow(DateOnly.FromDateTime(b.AddedUtc.UtcDateTime), from, to)),
            OldestBook = shelf.Where(b => b.PublishedYear is > 0).OrderBy(b => b.PublishedYear).ThenBy(b => b.Title).FirstOrDefault(),

            ReadAllTime = mine.Count(r => r.HasRead),
            ReadInPeriod = reads.Count(r => Overlaps(r.Date, from, to)),
            ReadingNow = mine.Count(r => r.Status == ReadingStatus.Reading),
            OnReadingList = queue.Count,
            UnreadOnShelf = shelf.Count(b => mineById.GetValueOrDefault(b.Id)?.HasRead != true),
            NextUp = queue.Select(r => byId[r.BookId]).FirstOrDefault(),
            LastFinished = reads.OrderByDescending(r => r.Date).FirstOrDefault(),
            ReadsPerYear = PerYear(reads, today),

            Rated = rated.Count,
            AverageRating = rated.Count == 0 ? null : Math.Round(rated.Average(r => r.Rating!.Value), 1),
            RatingCounts = ratingCounts,
            TopAuthors = [.. mine.Where(r => r.HasRead)
                .SelectMany(r => Authors(byId[r.BookId].Author))
                .GroupBy(a => a, StringComparer.OrdinalIgnoreCase)
                .Select(g => new AuthorCount(g.First(), g.Count()))
                .Where(a => a.Books > 1)
                .OrderByDescending(a => a.Books)
                .ThenBy(a => a.Author, StringComparer.OrdinalIgnoreCase)
                .Take(5)],
            FiveStarBooks = [.. rated.Where(r => r.Rating == 5)
                .OrderByDescending(r => r.Reads.Count == 0 ? null : r.Reads.Max())
                .ThenBy(r => byId[r.BookId].Title)
                .Select(r => byId[r.BookId])],
        };
    }

    /// <summary>My finished books in a calendar year (year-only and month-only dates count in their year), newest first.</summary>
    public static IReadOnlyList<ReadBook> ReadInYear(IReadOnlyList<Book> books, IReadOnlyList<Reading> myReadings, int year)
    {
        var byId = books.Where(b => !b.IsDeleted).ToDictionary(b => b.Id);
        return [.. myReadings
            .Where(r => byId.ContainsKey(r.BookId))
            .SelectMany(r => r.Reads.Where(d => d.Year == year).Select(d => new ReadBook(byId[r.BookId], r, d)))
            .OrderByDescending(r => r.Date)
            .ThenBy(r => r.Book.Title)];
    }

    /// <summary>Years I finished books in, newest first.</summary>
    public static IReadOnlyList<int> YearsWithReads(IReadOnlyList<Reading> myReadings) =>
        [.. myReadings.SelectMany(r => r.Reads).Select(d => d.Year).Distinct().OrderDescending()];

    /// <summary>"Jane Austen & Anna Smith" → both names, matching how the edit form separates authors.</summary>
    internal static IEnumerable<string> Authors(string author) =>
        author.Split(['&', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    internal static (DateOnly From, DateOnly To) Window(StatsPeriod period, DateOnly today) => period == StatsPeriod.CalendarYear
        ? (new DateOnly(today.Year, 1, 1), today)
        : (today.AddYears(-1).AddDays(1), today);

    private static bool InWindow(DateOnly date, DateOnly from, DateOnly to) => date >= from && date <= to;

    /// <summary>
    /// A vague date covers a span ("2026" is the whole year, "March 2026" the month); it counts when
    /// that span overlaps the window. So a year-only read always counts in its calendar year.
    /// </summary>
    internal static bool Overlaps(ReadDate date, DateOnly from, DateOnly to)
    {
        var start = new DateOnly(date.Year, date.Month ?? 1, date.Day ?? 1);
        var end = date.Day is not null ? start
            : date.Month is { } m ? new DateOnly(date.Year, m, DateTime.DaysInMonth(date.Year, m))
            : new DateOnly(date.Year, 12, 31);
        return start <= to && end >= from;
    }

    /// <summary>Books finished per year, oldest first, ending this year and showing up to the last 10 years (gaps as 0).</summary>
    private static IReadOnlyList<YearCount> PerYear(IReadOnlyList<ReadBook> reads, DateOnly today)
    {
        if (reads.Count == 0)
        {
            return [];
        }

        var last = Math.Max(today.Year, reads.Max(r => r.Date.Year));
        var first = Math.Max(reads.Min(r => r.Date.Year), last - MaxChartYears + 1);
        var counts = reads.GroupBy(r => r.Date.Year).ToDictionary(g => g.Key, g => g.Count());
        return [.. Enumerable.Range(first, last - first + 1).Select(y => new YearCount(y, counts.GetValueOrDefault(y)))];
    }
}

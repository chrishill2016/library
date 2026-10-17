using System.Globalization;
using System.Text.Json.Serialization;

namespace Bookshelf.Models;

/// <summary>
/// One person's rating, review and reading log for a book. Each person writes only their own
/// (<c>libraries/{id}/readings/{BookId}_{Uid}</c>), so two people never overwrite each other.
/// </summary>
public sealed record Reading
{
    /// <summary>Always <see cref="IdFor"/>(BookId, Uid); the security rules check it.</summary>
    public required string Id { get; init; }
    public required string BookId { get; init; }
    public required string Uid { get; init; }

    /// <summary>The reader's name when last saved, so the other person's phone can show it offline.</summary>
    public string ReaderName { get; init; } = "";

    /// <summary>1–5 stars, or null when not rated.</summary>
    public int? Rating { get; init; }
    public string? Review { get; init; }

    /// <summary>See <see cref="ReadingStatus"/>; null when not set.</summary>
    public string? Status { get; init; }

    /// <summary>
    /// Position on this person's reading list (smaller is sooner). Set while the status is
    /// <see cref="ReadingStatus.WantToRead"/>; fractional so a move rewrites only the moved book.
    /// </summary>
    public double? QueueOrder { get; init; }

    /// <summary>When it was read (newest first); re-reads add another.</summary>
    public IReadOnlyList<ReadDate> Reads { get; init; } = [];

    /// <summary>UTC time of the last change; used for last-write-wins sync.</summary>
    public DateTimeOffset LastModified { get; init; }

    public static string IdFor(string bookId, string uid) => $"{bookId}_{uid}";

    // Computed properties are not stored: firestore.rules accepts only the fields above.
    [JsonIgnore]
    public bool IsEmpty => Rating is null && Review is null && Status is null && Reads.Count == 0;

    [JsonIgnore]
    public bool IsQueued => Status == ReadingStatus.WantToRead;

    /// <summary>Finished at least once: marked as read or has a read date.</summary>
    [JsonIgnore]
    public bool HasRead => Status == ReadingStatus.Read || Reads.Count > 0;
}

/// <summary>Values for <see cref="Reading.Status"/> (strings so older clients ignore new ones).</summary>
public static class ReadingStatus
{
    public const string WantToRead = "want";
    public const string Reading = "reading";
    public const string Read = "read";

    public static string Label(string? status) => status switch
    {
        WantToRead => "Want to read",
        Reading => "Reading",
        Read => "Read",
        _ => "",
    };
}

/// <summary>A date that may be just a year, or a month and year, for reads remembered only roughly.</summary>
public sealed record ReadDate(int Year, int? Month = null, int? Day = null) : IComparable<ReadDate>
{
    /// <summary>A real date: a day needs a month, and both must exist in that year.</summary>
    [JsonIgnore]
    public bool IsValid =>
        Year is >= 1900 and <= 2200
        && (Month is null ? Day is null : Month is >= 1 and <= 12)
        && (Day is null || (Month is { } m && Day >= 1 && Day <= DateTime.DaysInMonth(Year, m)));

    public static ReadDate On(DateOnly date) => new(date.Year, date.Month, date.Day);

    /// <summary>"12 March 2015", "March 2015" or "2015".</summary>
    public override string ToString()
    {
        var month = Month is { } m ? CultureInfo.GetCultureInfo("en-GB").DateTimeFormat.GetMonthName(m) : null;
        return (Day, month) switch
        {
            ({ } d, { } name) => $"{d} {name} {Year}",
            (null, { } name) => $"{name} {Year}",
            _ => Year.ToString(CultureInfo.InvariantCulture),
        };
    }

    /// <summary>Oldest first; a vaguer date sorts before more precise ones in the same period.</summary>
    public int CompareTo(ReadDate? other) =>
        other is null ? 1 : (Year, Month ?? 0, Day ?? 0).CompareTo((other.Year, other.Month ?? 0, other.Day ?? 0));
}

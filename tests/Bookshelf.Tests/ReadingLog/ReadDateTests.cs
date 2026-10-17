using Bookshelf.Models;

namespace Bookshelf.Tests.ReadingLog;

public class ReadDateTests
{
    [Theory]
    [InlineData(2015, null, null, "2015")]
    [InlineData(2015, 3, null, "March 2015")]
    [InlineData(2015, 3, 12, "12 March 2015")]
    public void Shows_only_what_is_known(int year, int? month, int? day, string expected) =>
        Assert.Equal(expected, new ReadDate(year, month, day).ToString());

    [Theory]
    [InlineData(2024, 2, 29, true)]
    [InlineData(2023, 2, 29, false)]
    [InlineData(2023, null, 5, false)]
    [InlineData(2023, 13, null, false)]
    [InlineData(1850, null, null, false)]
    [InlineData(2023, null, null, true)]
    public void Validates_real_dates(int year, int? month, int? day, bool valid) =>
        Assert.Equal(valid, new ReadDate(year, month, day).IsValid);

    [Fact]
    public void Sorts_chronologically_with_vaguer_dates_first_in_their_period()
    {
        ReadDate[] dates = [new(2020, 5, 1), new(2019), new(2020), new(2020, 5)];

        Assert.Equal([new(2019), new(2020), new(2020, 5), new(2020, 5, 1)], dates.Order());
    }
}

public class ReadingSerializationTests
{
    /// <summary>
    /// Blazor sends records to JS (and on to Firestore) with web JSON defaults. The security rules
    /// accept only these keys (plus syncedAt, added in firebase.js), so an extra one breaks every push.
    /// </summary>
    [Fact]
    public void Serializes_exactly_the_fields_the_rules_allow()
    {
        var reading = Bookshelf.Tests.Fakes.TestReadings.Make("b1", "chris", rating: 4, status: ReadingStatus.Read,
            review: "Good", name: "Chris", reads: new ReadDate(2015, 3));

        var json = System.Text.Json.JsonSerializer.SerializeToElement(reading, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Equal(
            ["bookId", "id", "lastModified", "queueOrder", "rating", "readerName", "reads", "review", "status", "uid"],
            json.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(["day", "month", "year"], json.GetProperty("reads")[0].EnumerateObject().Select(p => p.Name).Order());
    }
}

using Bookshelf.Models;

namespace Bookshelf.Tests.Fakes;

public static class TestBooks
{
    public static readonly DateTimeOffset Epoch = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static Book Make(string id, string title = "A Book", string author = "", int minutes = 0, bool deleted = false) => new()
    {
        Id = id,
        Title = title,
        Author = author,
        AddedUtc = Epoch,
        LastModified = Epoch.AddMinutes(minutes),
        IsDeleted = deleted,
    };
}

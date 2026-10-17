using Bookshelf.Models;

namespace Bookshelf.Tests.Fakes;

public static class TestReadings
{
    public static Reading Make(string bookId, string uid, int? rating = null, string? status = null, int minutes = 0,
                               string? review = null, string name = "", params ReadDate[] reads) => new()
    {
        Id = Reading.IdFor(bookId, uid),
        BookId = bookId,
        Uid = uid,
        ReaderName = name,
        Rating = rating,
        Status = status,
        Review = review,
        Reads = reads,
        LastModified = TestBooks.Epoch.AddMinutes(minutes),
    };
}

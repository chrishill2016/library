using Bookshelf.Features.Books;

namespace Bookshelf.Tests.Books;

public class IsbnTests
{
    [Theory]
    [InlineData("978-0-261-10221-7")]
    [InlineData("9780261102217")]
    [InlineData("0-261-10221-4")]
    [InlineData("0 8044 2957 X")]
    [InlineData("080442957x")]
    public void Valid_isbns(string isbn) => Assert.True(Isbn.IsValid(isbn));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("978-0-261-10221-8")]
    [InlineData("0-261-10221-5")]
    [InlineData("12345")]
    [InlineData("97802611022X7")]
    [InlineData("X261102214")]
    public void Invalid_isbns(string? isbn) => Assert.False(Isbn.IsValid(isbn));

    [Fact]
    public void Normalize_strips_separators_and_uppercases_x() =>
        Assert.Equal("080442957X", Isbn.Normalize(" 0-8044-2957-x "));
}

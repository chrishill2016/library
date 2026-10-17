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
    [InlineData("5000000000005")] // valid EAN-13 checksum, but not a 978/979 book number
    public void Invalid_isbns(string? isbn) => Assert.False(Isbn.IsValid(isbn));

    [Fact]
    public void Normalize_strips_separators_and_uppercases_x() =>
        Assert.Equal("080442957X", Isbn.Normalize(" 0-8044-2957-x "));

    [Theory]
    [InlineData("0-261-10221-4", "9780261102217")]
    [InlineData("080442957X", "9780804429573")]
    [InlineData("978-0-261-10221-7", "9780261102217")]
    [InlineData("12345", null)]
    [InlineData(null, null)]
    public void To_isbn13(string? input, string? expected) => Assert.Equal(expected, Isbn.ToIsbn13(input));

    [Theory]
    [InlineData("9780261102217", true)]
    [InlineData("9791032305690", true)]
    [InlineData("5012345678900", false)]
    [InlineData("9780261102218", false)]
    [InlineData("0261102214", false)]
    public void Book_barcodes(string code, bool expected) => Assert.Equal(expected, Isbn.IsBookBarcode(code));
}

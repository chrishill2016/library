namespace Bookshelf.Features.Books;

public static class Isbn
{
    /// <summary>Strips spaces and hyphens and upper-cases a trailing X.</summary>
    public static string Normalize(string? isbn) =>
        string.IsNullOrWhiteSpace(isbn)
            ? ""
            : new string(isbn.Where(c => char.IsDigit(c) || c is 'x' or 'X').ToArray()).ToUpperInvariant();

    /// <summary>True for a valid ISBN-10 or ISBN-13 (checksum included).</summary>
    public static bool IsValid(string? isbn)
    {
        var digits = Normalize(isbn);
        return digits.Length switch
        {
            10 => IsValidIsbn10(digits),
            13 => IsValidIsbn13(digits),
            _ => false,
        };
    }

    /// <summary>
    /// The ISBN-13 form of a valid ISBN-10 or ISBN-13, used to compare books
    /// regardless of which form was typed or scanned. Null when not a valid ISBN.
    /// </summary>
    public static string? ToIsbn13(string? isbn)
    {
        var digits = Normalize(isbn);
        if (!IsValid(digits))
        {
            return null;
        }

        if (digits.Length == 13)
        {
            return digits;
        }

        var body = "978" + digits[..9];
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            sum += (body[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return body + (char)('0' + (10 - sum % 10) % 10);
    }

    /// <summary>
    /// True when a scanned EAN-13 barcode is a book ISBN ("Bookland": 978/979 prefix,
    /// valid checksum). Filters out price codes and other barcodes on the cover.
    /// </summary>
    public static bool IsBookBarcode(string? code)
    {
        var digits = Normalize(code);
        return digits.Length == 13 && IsValidIsbn13(digits);
    }

    private static bool IsValidIsbn10(string digits)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            int value;
            if (digits[i] == 'X')
            {
                if (i != 9)
                {
                    return false;
                }

                value = 10;
            }
            else
            {
                value = digits[i] - '0';
            }

            sum += value * (10 - i);
        }

        return sum % 11 == 0;
    }

    private static bool IsValidIsbn13(string digits)
    {
        // ISBN-13s are "Bookland" EANs: other 13-digit barcodes (prices, groceries) aren't books.
        if (digits.Contains('X') || !(digits.StartsWith("978") || digits.StartsWith("979")))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 13; i++)
        {
            sum += (digits[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return sum % 10 == 0;
    }
}

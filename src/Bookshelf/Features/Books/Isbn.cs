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
        if (digits.Contains('X'))
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

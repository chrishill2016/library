using Bookshelf.Features.Books;
using Bookshelf.Features.Lookup;

namespace Bookshelf.Tests.Fakes;

public sealed class FakeBookLookup : IBookLookup
{
    public Dictionary<string, BookCandidate> Known { get; } = [];
    public List<BookCandidate> SearchResults { get; } = [];
    public bool Unavailable { get; set; }
    public List<string> Lookups { get; } = [];
    public Dictionary<string, string> ProblemsBySource { get; } = [];

    public IReadOnlyDictionary<string, string> Problems => ProblemsBySource;

    public FakeBookLookup Add(string isbn13, string title, string author = "", string? cover = null)
    {
        Known[isbn13] = new BookCandidate(title, author, isbn13, null, 2001, cover, "Fake");
        return this;
    }

    public Task<LookupResult> LookupIsbnAsync(string isbn, CancellationToken cancellationToken = default)
    {
        Lookups.Add(isbn);
        if (Unavailable)
        {
            return Task.FromResult(LookupResult.Unavailable);
        }

        return Task.FromResult(Known.TryGetValue(Isbn.ToIsbn13(isbn) ?? "", out var found)
            ? new LookupResult(LookupOutcome.Found, found)
            : LookupResult.NotFound);
    }

    public Task<IReadOnlyList<BookCandidate>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BookCandidate>>(Unavailable ? [] : SearchResults);
}

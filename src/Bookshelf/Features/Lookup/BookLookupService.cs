using Bookshelf.Features.Books;
using Microsoft.Extensions.Configuration;

namespace Bookshelf.Features.Lookup;

/// <summary>Settings from the "BookLookup" section of appsettings.json.</summary>
public sealed record LookupOptions(string? GoogleBooksApiKey)
{
    public static LookupOptions FromConfiguration(IConfiguration configuration) =>
        new(configuration["BookLookup:GoogleBooksApiKey"]);
}

public interface IBookLookup
{
    Task<LookupResult> LookupIsbnAsync(string isbn, CancellationToken cancellationToken = default);

    /// <summary>Books matching title and/or author text, best first. Empty when nothing found or offline.</summary>
    Task<IReadOnlyList<BookCandidate>> SearchAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>The latest error from each source that refused to answer this session (e.g. Google's daily quota), by source name.</summary>
    IReadOnlyDictionary<string, string> Problems { get; }
}

/// <summary>
/// Asks each source in order (Google Books, then Open Library) and merges what
/// they know: the first source's details win, later sources fill the gaps
/// (often the cover).
/// </summary>
public sealed class BookLookupService(IEnumerable<IBookSource> sources) : IBookLookup
{
    private readonly IReadOnlyList<IBookSource> _sources = [.. sources];
    private readonly Dictionary<string, string> _problems = [];

    public IReadOnlyDictionary<string, string> Problems => _problems;

    public async Task<LookupResult> LookupIsbnAsync(string isbn, CancellationToken cancellationToken = default)
    {
        var isbn13 = Isbn.ToIsbn13(isbn);
        if (isbn13 is null)
        {
            return LookupResult.NotFound;
        }

        var answers = await Task.WhenAll(_sources.Select(s => TryAsync(() => s.FindByIsbnAsync(isbn13, cancellationToken))));

        BookCandidate? merged = null;
        foreach (var (ok, found) in answers)
        {
            if (ok && found is not null)
            {
                merged = merged is null ? found : merged.FillFrom(found);
            }
        }

        if (merged is not null)
        {
            if (merged.Synopsis is null)
            {
                // Google had none (or didn't answer): try sources that keep descriptions elsewhere.
                foreach (var source in _sources.OfType<ISynopsisSource>())
                {
                    var (ok, synopsis) = await TryAsync(() => source.FindSynopsisAsync(isbn13, cancellationToken));
                    if (ok && synopsis is not null)
                    {
                        merged = merged with { Synopsis = synopsis };
                        break;
                    }
                }
            }

            return new LookupResult(LookupOutcome.Found, merged with { Isbn = isbn13 });
        }

        // Only call it "not found" if every source actually answered.
        return answers.All(a => a.Ok) ? LookupResult.NotFound : LookupResult.Unavailable;
    }

    public async Task<IReadOnlyList<BookCandidate>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var answers = await Task.WhenAll(_sources.Select(s => TryAsync(() => s.SearchAsync(query.Trim(), cancellationToken))));

        // Keep each source's ranking, first source first; merge duplicates into the earlier entry.
        var results = new List<BookCandidate>();
        var index = new Dictionary<string, int>();
        foreach (var (_, list) in answers)
        {
            foreach (var candidate in list ?? [])
            {
                if (index.TryGetValue(candidate.MatchKey, out var at))
                {
                    results[at] = results[at].FillFrom(candidate);
                }
                else
                {
                    index[candidate.MatchKey] = results.Count;
                    results.Add(candidate);
                }
            }
        }

        return results;
    }

    private async Task<(bool Ok, T? Value)> TryAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return (true, await call());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BookSourceException ex)
        {
            // The source answered with a reason (quota, key restrictions): keep it so Settings can say why.
            _problems[ex.Source] = ex.Message[(ex.Source.Length + 2)..];
            return (false, default);
        }
        catch (Exception)
        {
            // Offline, unexpected JSON… treat as "this source is unavailable".
            return (false, default);
        }
    }
}

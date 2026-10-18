using Bookshelf.Features.Books;
using Bookshelf.Models;
using Bookshelf.Services.Browser;

namespace Bookshelf.Features.Lookup;

/// <summary>
/// One-off job (Settings) that looks up books missing a synopsis or genres and fills the gaps.
/// Never overwrites what's there. Books with an ISBN are looked up by it; others by title and
/// author, accepting only an exact title match by the same first author. Saves quietly and syncs
/// in batches, so a library of hundreds doesn't sync (and re-read Firestore) after every book.
/// </summary>
public sealed class BookDetailsFiller(BookService books, IBookLookup lookup, IConnectivityService connectivity)
{
    private const int SyncEvery = 25;

    private CancellationTokenSource? _cts;

    /// <summary>Pause between books, to be polite to the free APIs (and their daily quotas).</summary>
    public TimeSpan Pause { get; set; } = TimeSpan.FromMilliseconds(300);

    public bool IsRunning { get; private set; }
    public int Checked { get; private set; }
    public int Filled { get; private set; }
    public int Total { get; private set; }

    /// <summary>Why the last run stopped early (offline, sources unreachable), or null.</summary>
    public string? StoppedReason { get; private set; }

    public event Action? Changed;

    public static bool IsMissingDetails(Book book) =>
        !book.IsDeleted && book.LookupState is null && (string.IsNullOrWhiteSpace(book.Synopsis) || book.Genres.Count == 0);

    public async Task<int> CountMissingAsync() => (await books.GetAllAsync()).Count(IsMissingDetails);

    public void Stop() => _cts?.Cancel();

    public async Task RunAsync()
    {
        if (IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        IsRunning = true;
        StoppedReason = null;
        Checked = Filled = 0;
        var todo = (await books.GetAllAsync()).Where(IsMissingDetails).ToList();
        Total = todo.Count;
        Changed?.Invoke();

        try
        {
            foreach (var book in todo)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (!connectivity.IsOnline)
                {
                    StoppedReason = "You went offline. Run it again when you're back online to carry on.";
                    break;
                }

                var (found, unavailable) = await FindAsync(book, token);
                if (unavailable)
                {
                    StoppedReason = "Couldn't reach the book sites (or today's lookup limit was reached). Try again later to carry on.";
                    break;
                }

                // Someone may have edited the book meanwhile: fill only what's still missing.
                if (found is not null && await books.GetAsync(book.Id) is { } current)
                {
                    var synopsis = string.IsNullOrWhiteSpace(current.Synopsis) ? found.Synopsis : current.Synopsis;
                    var genres = current.Genres.Count > 0 ? current.Genres : found.SuggestedGenres;
                    if (synopsis != current.Synopsis || !genres.SequenceEqual(current.Genres))
                    {
                        await books.SaveAsync(current with { Synopsis = synopsis, Genres = genres }, syncNow: false);
                        Filled++;
                        if (Filled % SyncEvery == 0)
                        {
                            await books.SyncAsync();
                        }
                    }
                }

                Checked++;
                Changed?.Invoke();
                if (Pause > TimeSpan.Zero)
                {
                    await Task.Delay(Pause, token).ContinueWith(_ => { }, TaskScheduler.Default);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped by the user.
        }
        finally
        {
            IsRunning = false;
            if (Filled > 0)
            {
                await books.SyncAsync();
            }

            Changed?.Invoke();
        }
    }

    private async Task<(BookCandidate? Found, bool Unavailable)> FindAsync(Book book, CancellationToken token)
    {
        if (Isbn.IsValid(book.Isbn))
        {
            var result = await lookup.LookupIsbnAsync(book.Isbn!, token);
            return (result.Book, result.Outcome == LookupOutcome.Unavailable);
        }

        if (string.IsNullOrWhiteSpace(book.Title))
        {
            return (null, false);
        }

        var candidates = await lookup.SearchAsync($"{book.Title} {book.Author}".Trim(), token);
        return (candidates.FirstOrDefault(c => SameBook(c, book)), false);
    }

    /// <summary>Exact title (ignoring case and punctuation) and, when we know it, the same first author's surname.</summary>
    internal static bool SameBook(BookCandidate candidate, Book book)
    {
        static string Simple(string text) => new([.. text.ToLowerInvariant().Where(char.IsLetterOrDigit)]);
        static string Surname(string author) =>
            Simple(author.Split('&')[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "");

        return Simple(candidate.Title) == Simple(book.Title)
            && (string.IsNullOrWhiteSpace(book.Author) || Surname(candidate.Author) == Surname(book.Author));
    }
}

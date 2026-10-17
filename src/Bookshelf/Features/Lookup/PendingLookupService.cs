using Bookshelf.Features.Books;
using Bookshelf.Models;
using Bookshelf.Services.Browser;

namespace Bookshelf.Features.Lookup;

/// <summary>
/// Fills in books that were scanned offline (saved by ISBN only) once the
/// device is back online. Runs when the library opens and on reconnect.
/// </summary>
public sealed class PendingLookupService : IDisposable
{
    private readonly BookService _books;
    private readonly IBookLookup _lookup;
    private readonly IConnectivityService _connectivity;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PendingLookupService(BookService books, IBookLookup lookup, IConnectivityService connectivity)
    {
        _books = books;
        _lookup = lookup;
        _connectivity = connectivity;
        _connectivity.Changed += OnConnectivityChanged;
    }

    /// <summary>Looks up every pending book. Returns how many were resolved (found or not found).</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!_connectivity.IsOnline || !await _gate.WaitAsync(0, cancellationToken))
        {
            // Offline, or a run is already in progress.
            return 0;
        }

        try
        {
            var pending = (await _books.GetAllAsync()).Where(b => b.LookupState == BookLookupState.Pending).ToList();
            var resolved = 0;
            foreach (var book in pending)
            {
                var result = await _lookup.LookupIsbnAsync(book.Isbn ?? "", cancellationToken);
                if (result.Outcome == LookupOutcome.Unavailable)
                {
                    // Sources unreachable: try again on the next run.
                    break;
                }

                // Someone may have edited it meanwhile; only fill books still pending.
                var current = await _books.GetAsync(book.Id);
                if (current?.LookupState != BookLookupState.Pending)
                {
                    continue;
                }

                await _books.SaveAsync(result.Book is { } found
                    ? current with
                    {
                        Title = found.Title,
                        Author = found.Author,
                        Publisher = found.Publisher,
                        PublishedYear = found.PublishedYear,
                        CoverUrl = found.CoverUrl,
                        LookupState = null,
                    }
                    : current with { LookupState = BookLookupState.NotFound });
                resolved++;
            }

            return resolved;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async void OnConnectivityChanged(bool isOnline)
    {
        if (!isOnline)
        {
            return;
        }

        try
        {
            await RunAsync();
        }
        catch (Exception)
        {
            // Background best effort; books stay pending and are retried later.
        }
    }

    public void Dispose()
    {
        _connectivity.Changed -= OnConnectivityChanged;
        _gate.Dispose();
    }
}

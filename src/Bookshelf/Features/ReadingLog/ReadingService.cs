using Bookshelf.Features.Account;
using Bookshelf.Models;
using Bookshelf.Services.Storage;
using Bookshelf.Services.Sync;

namespace Bookshelf.Features.ReadingLog;

/// <summary>
/// Ratings, reviews, status and read dates. Everyone's are visible; only the signed-in
/// person's own can be changed. Writes go to the local store first and sync like books.
/// </summary>
public sealed class ReadingService : IDisposable
{
    public const int MaxReviewLength = 10_000;
    public const int MaxReads = 100;

    private readonly ILocalStore _store;
    private readonly ISyncService _sync;
    private readonly AuthService _auth;
    private readonly TimeProvider _time;
    private List<Reading>? _cache;
    private string? _cachedUid;

    public ReadingService(ILocalStore store, ISyncService sync, AuthService auth, TimeProvider time)
    {
        _store = store;
        _sync = sync;
        _auth = auth;
        _time = time;
        _sync.RemoteChangesApplied += Invalidate;
        _auth.Changed += OnAuthChanged;
    }

    /// <summary>Raised when readings change locally or from sync.</summary>
    public event Action? Changed;

    public string? MyUid => _auth.User?.Uid;

    public async Task<IReadOnlyList<Reading>> GetAllAsync()
    {
        _cache ??= [.. await _store.GetReadingsAsync()];
        return _cache;
    }

    /// <summary>The signed-in person's readings, keyed by book id.</summary>
    public async Task<IReadOnlyDictionary<string, Reading>> GetMyReadingsAsync()
    {
        var uid = MyUid;
        return uid is null
            ? new Dictionary<string, Reading>()
            : (await GetAllAsync()).Where(r => r.Uid == uid).ToDictionary(r => r.BookId);
    }

    /// <summary>The signed-in person's reading of a book; empty (and unsaved) if they haven't recorded anything.</summary>
    public async Task<Reading> GetMyReadingAsync(string bookId)
    {
        var uid = RequireUser().Uid;
        return (await GetAllAsync()).FirstOrDefault(r => r.BookId == bookId && r.Uid == uid)
               ?? new Reading { Id = Reading.IdFor(bookId, uid), BookId = bookId, Uid = uid };
    }

    /// <summary>Other people's readings of a book that have something in them.</summary>
    public async Task<IReadOnlyList<Reading>> GetOthersAsync(string bookId)
    {
        var uid = MyUid;
        return [.. (await GetAllAsync())
            .Where(r => r.BookId == bookId && r.Uid != uid && !r.IsEmpty)
            .OrderBy(r => r.ReaderName, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>Sets 1–5 stars, or clears the rating with null.</summary>
    public Task<Reading> SetRatingAsync(string bookId, int? rating)
    {
        if (rating is not (null or (>= 1 and <= 5)))
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "A rating is 1 to 5 stars.");
        }

        return UpdateAsync(bookId, r => r with { Rating = rating });
    }

    /// <summary>Sets the status. "Want to read" puts the book at the end of the reading list; anything else takes it off.</summary>
    public Task<Reading> SetStatusAsync(string bookId, string? status)
    {
        if (status is not (null or ReadingStatus.WantToRead or ReadingStatus.Reading or ReadingStatus.Read))
        {
            throw new ArgumentException($"Unknown reading status '{status}'.", nameof(status));
        }

        return UpdateAsync(bookId, r => r with { Status = status });
    }

    /// <summary>The person's reading list (want-to-read books), soonest first.</summary>
    public async Task<IReadOnlyList<Reading>> GetQueueAsync(string uid) =>
        [.. (await GetAllAsync())
            .Where(r => r.Uid == uid && r.IsQueued)
            .OrderBy(r => r.QueueOrder ?? double.MaxValue)
            .ThenBy(r => r.LastModified)];

    /// <summary>Other people who have something on their reading list: (uid, name).</summary>
    public async Task<IReadOnlyList<(string Uid, string Name)>> GetOtherListsAsync()
    {
        var uid = MyUid;
        return [.. (await GetAllAsync())
            .Where(r => r.Uid != uid && r.IsQueued)
            .GroupBy(r => r.Uid)
            .Select(g => (g.Key, g.OrderByDescending(r => r.LastModified).First().ReaderName))
            .OrderBy(x => x.ReaderName, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>Moves a book on my reading list to a new position (0 = next to read).</summary>
    public async Task MoveInQueueAsync(string bookId, int toIndex)
    {
        var queue = (await GetQueueAsync(RequireUser().Uid)).ToList();
        var from = queue.FindIndex(r => r.BookId == bookId);
        if (from < 0)
        {
            return;
        }

        var moving = queue[from];
        queue.RemoveAt(from);
        toIndex = Math.Clamp(toIndex, 0, queue.Count);
        if (toIndex == from)
        {
            return;
        }

        var before = toIndex > 0 ? queue[toIndex - 1].QueueOrder : null;
        var after = toIndex < queue.Count ? queue[toIndex].QueueOrder : null;
        var order = (before, after) switch
        {
            (null, null) when queue.Count == 0 => 1,
            ({ } b, { } a) when a - b > 1e-6 => (a + b) / 2,
            ({ } b, null) when toIndex == queue.Count => b + 1,
            (null, { } a) when toIndex == 0 => a - 1,
            _ => (double?)null,
        };

        if (order is { } value)
        {
            await UpdateAsync(bookId, r => r with { QueueOrder = value });
            return;
        }

        // Positions too close together (or missing): renumber the whole list.
        queue.Insert(toIndex, moving);
        for (var i = 0; i < queue.Count; i++)
        {
            var position = i + 1;
            if (queue[i].QueueOrder != position)
            {
                await UpdateAsync(queue[i].BookId, r => r with { QueueOrder = position });
            }
        }
    }

    private async Task<double> EndOfQueueAsync()
    {
        var uid = MyUid;
        return uid is null ? 1 : (await GetQueueAsync(uid)).Select(r => r.QueueOrder ?? 0).DefaultIfEmpty(0).Max() + 1;
    }

    public Task<Reading> SetReviewAsync(string bookId, string? review)
    {
        var cleaned = string.IsNullOrWhiteSpace(review) ? null : review.Trim();
        if (cleaned?.Length > MaxReviewLength)
        {
            throw new ArgumentException($"Reviews can be up to {MaxReviewLength:N0} characters.", nameof(review));
        }

        return UpdateAsync(bookId, r => r with { Review = cleaned });
    }

    /// <summary>Records finishing the book on a date (re-reads add another) and marks it read.</summary>
    public Task<Reading> AddReadAsync(string bookId, ReadDate date)
    {
        if (!date.IsValid)
        {
            throw new ArgumentException("That isn't a real date.", nameof(date));
        }

        return UpdateAsync(bookId, r =>
        {
            if (r.Reads.Contains(date))
            {
                return r with { Status = ReadingStatus.Read };
            }

            if (r.Reads.Count >= MaxReads)
            {
                throw new InvalidOperationException($"A book can have up to {MaxReads} read dates.");
            }

            return r with { Reads = [.. r.Reads, date], Status = ReadingStatus.Read };
        });
    }

    public Task<Reading> RemoveReadAsync(string bookId, ReadDate date) =>
        UpdateAsync(bookId, r => r with { Reads = [.. r.Reads.Where(d => d != date)] });

    private async Task<Reading> UpdateAsync(string bookId, Func<Reading, Reading> change)
    {
        var user = RequireUser();
        var changed = change(await GetMyReadingAsync(bookId));
        var updated = changed with
        {
            ReaderName = user.Name,
            Reads = [.. changed.Reads.OrderDescending()],
            // On the reading list exactly while "want to read"; newly wanted books go to the end.
            QueueOrder = !changed.IsQueued ? null : changed.QueueOrder ?? await EndOfQueueAsync(),
            LastModified = _time.GetUtcNow(),
        };

        await _store.PutReadingAsync(updated, OutboxEntry.ForReading(updated.Id, updated.LastModified));
        _cache = null;
        Changed?.Invoke();
        await _sync.NotifyLocalChangeAsync();
        return updated;
    }

    private Services.Firebase.AppUser RequireUser() =>
        _auth.User ?? throw new InvalidOperationException("Sign in to record your reading.");

    /// <summary>Drops the cached list, e.g. after sync wrote to the local store.</summary>
    public void Invalidate()
    {
        _cache = null;
        Changed?.Invoke();
    }

    private void OnAuthChanged()
    {
        if (_cachedUid != MyUid)
        {
            _cachedUid = MyUid;
            Invalidate();
        }
    }

    public void Dispose()
    {
        _sync.RemoteChangesApplied -= Invalidate;
        _auth.Changed -= OnAuthChanged;
    }
}

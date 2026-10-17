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

    public Task<Reading> SetStatusAsync(string bookId, string? status)
    {
        if (status is not (null or ReadingStatus.WantToRead or ReadingStatus.Reading or ReadingStatus.Read))
        {
            throw new ArgumentException($"Unknown reading status '{status}'.", nameof(status));
        }

        return UpdateAsync(bookId, r => r with { Status = status });
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

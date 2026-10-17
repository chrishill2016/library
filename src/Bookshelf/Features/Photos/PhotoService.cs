using Bookshelf.Features.Books;
using Bookshelf.Models;
using Bookshelf.Services.Browser;
using Bookshelf.Services.Firebase;

namespace Bookshelf.Features.Photos;

/// <summary>
/// Attaches cover photos to books and keeps Firebase Storage in step: photos are
/// saved on the device first and uploaded when online; replaced or removed photos
/// (and those of deleted books) are deleted from Storage.
/// </summary>
public sealed class PhotoService : IDisposable
{
    private readonly IPhotoStore _photos;
    private readonly BookService _books;
    private readonly RemoteSession _session;
    private readonly IConnectivityService _connectivity;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PhotoService(IPhotoStore photos, BookService books, RemoteSession session, IConnectivityService connectivity)
    {
        _photos = photos;
        _books = books;
        _session = session;
        _connectivity = connectivity;
        _connectivity.Changed += OnConnectivityChanged;
        _session.Changed += OnSessionChanged;
    }

    /// <summary>Photos waiting to upload or be deleted remotely.</summary>
    public int PendingCount { get; private set; }

    public event Action? Changed;

    public Task<CapturedPhoto?> CaptureAsync(Microsoft.AspNetCore.Components.ElementReference fileInput) =>
        _photos.CaptureAsync(fileInput);

    /// <summary>URL to show for a book's photo, or null (fall back to the online cover).</summary>
    public Task<string?> GetUrlAsync(string photoId) => _photos.GetUrlAsync(photoId, _session.LibraryId);

    /// <summary>Gives a saved book a new photo, replacing (and deleting) any previous one.</summary>
    public async Task<Book> SetPhotoAsync(Book book, string photoId)
    {
        var saved = await _books.SaveAsync(book with { PhotoId = photoId });
        await PhotoChangedAsync(previous: book.PhotoId, current: photoId);
        return saved;
    }

    public async Task<Book> RemovePhotoAsync(Book book)
    {
        if (book.PhotoId is null)
        {
            return book;
        }

        var saved = await _books.SaveAsync(book with { PhotoId = null });
        await PhotoChangedAsync(previous: book.PhotoId, current: null);
        return saved;
    }

    /// <summary>
    /// Call after saving a book whose photo may have changed (e.g. the edit form).
    /// Attaches the new photo and deletes the one it replaced.
    /// </summary>
    public async Task PhotoChangedAsync(string? previous, string? current)
    {
        if (current is not null)
        {
            await _photos.MarkAttachedAsync(current);
        }

        if (previous is not null && previous != current)
        {
            await _photos.MarkForDeletionAsync(previous);
        }

        await SyncAsync();
    }

    /// <summary>Deletes a book along with its photo.</summary>
    public async Task DeleteBookAsync(Book book)
    {
        await _books.DeleteAsync(book.Id);
        if (book.PhotoId is not null)
        {
            await _photos.MarkForDeletionAsync(book.PhotoId);
            await SyncAsync();
        }
    }

    /// <summary>Uploads/deletes queued photos when online and in a library. Never throws.</summary>
    public async Task SyncAsync()
    {
        if (_session.LibraryId is not { } libraryId || !_connectivity.IsOnline || !await _gate.WaitAsync(0))
        {
            await RefreshPendingAsync();
            return;
        }

        try
        {
            await _photos.ProcessQueueAsync(libraryId);
        }
        catch (Exception)
        {
            // Offline mid-way or Storage unavailable: everything stays queued for next time.
        }
        finally
        {
            _gate.Release();
        }

        await RefreshPendingAsync();
    }

    private async Task RefreshPendingAsync()
    {
        int pending;
        try
        {
            pending = await _photos.PendingCountAsync();
        }
        catch (Exception)
        {
            return;
        }

        if (pending != PendingCount)
        {
            PendingCount = pending;
            Changed?.Invoke();
        }
    }

    private async void OnConnectivityChanged(bool isOnline)
    {
        if (isOnline)
        {
            await SyncAsync();
        }
    }

    private async void OnSessionChanged() => await SyncAsync();

    public void Dispose()
    {
        _connectivity.Changed -= OnConnectivityChanged;
        _session.Changed -= OnSessionChanged;
        _gate.Dispose();
    }
}

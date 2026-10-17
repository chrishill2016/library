using Bookshelf.Features.Books;
using Bookshelf.Features.Photos;
using Bookshelf.Models;
using Bookshelf.Services.Firebase;
using Bookshelf.Services.Sync;
using Bookshelf.Tests.Fakes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Time.Testing;

namespace Bookshelf.Tests.Photos;

public class PhotoServiceTests
{
    private readonly InMemoryLocalStore _store = new();
    private readonly FakeConnectivity _connectivity = new();
    private readonly FakePhotoStore _photos = new();
    private readonly RemoteSession _session = new();
    private readonly BookService _books;
    private readonly PhotoService _sut;

    public PhotoServiceTests()
    {
        var time = new FakeTimeProvider(TestBooks.Epoch);
        var sync = new SyncService(_store, new NotConfiguredRemoteStore(), _connectivity, time);
        _books = new BookService(_store, sync, time);
        _sut = new PhotoService(_photos, _books, _session, _connectivity);
        _session.SetLibrary("lib1");
    }

    private async Task<Book> BookAsync() => await _books.SaveAsync(TestBooks.Make("b1", "Emma"));

    private async Task<string> CaptureAsync() => (await _sut.CaptureAsync(default))!.Id;

    [Fact]
    public async Task Setting_a_photo_saves_the_book_and_uploads()
    {
        var book = await BookAsync();
        var photoId = await CaptureAsync();

        var saved = await _sut.SetPhotoAsync(book, photoId);

        Assert.Equal(photoId, saved.PhotoId);
        Assert.Equal(photoId, _store.Books["b1"].PhotoId);
        Assert.Contains(photoId, _photos.Remote);
        Assert.Equal(0, _sut.PendingCount);
    }

    [Fact]
    public async Task Offline_photo_waits_then_uploads_on_reconnect()
    {
        _connectivity.Set(false);
        _photos.Offline = true;
        var photoId = await CaptureAsync();

        await _sut.SetPhotoAsync(await BookAsync(), photoId);

        Assert.Empty(_photos.Remote);
        Assert.Equal(1, _sut.PendingCount);
        Assert.Equal("blob:" + photoId, await _sut.GetUrlAsync(photoId));

        _photos.Offline = false;
        _connectivity.Set(true);
        await WaitForAsync(() => _photos.Remote.Contains(photoId));

        Assert.Equal(0, _sut.PendingCount);
    }

    [Fact]
    public async Task Replacing_a_photo_deletes_the_old_one()
    {
        var first = await CaptureAsync();
        var book = await _sut.SetPhotoAsync(await BookAsync(), first);
        var second = await CaptureAsync();

        await _sut.SetPhotoAsync(book, second);

        Assert.Equal([second], _photos.Remote);
        Assert.False(_photos.Local.ContainsKey(first));
    }

    [Fact]
    public async Task Removing_a_photo_clears_the_book_and_deletes_the_file()
    {
        var photoId = await CaptureAsync();
        var book = await _sut.SetPhotoAsync(await BookAsync(), photoId);

        var saved = await _sut.RemovePhotoAsync(book);

        Assert.Null(saved.PhotoId);
        Assert.Empty(_photos.Remote);
    }

    [Fact]
    public async Task Deleting_a_book_deletes_its_photo()
    {
        var photoId = await CaptureAsync();
        var book = await _sut.SetPhotoAsync(await BookAsync(), photoId);

        await _sut.DeleteBookAsync(book);

        Assert.True(_store.Books["b1"].IsDeleted);
        Assert.Empty(_photos.Remote);
    }

    [Fact]
    public async Task Photo_taken_in_the_form_but_never_saved_is_not_uploaded()
    {
        await CaptureAsync();

        await _sut.SyncAsync();

        Assert.Empty(_photos.Remote);
    }

    [Fact]
    public async Task Form_save_attaches_new_photo_and_deletes_replaced_one()
    {
        var old = await CaptureAsync();
        await _sut.SetPhotoAsync(await BookAsync(), old);
        var fresh = await CaptureAsync();

        await _sut.PhotoChangedAsync(previous: old, current: fresh);

        Assert.Equal([fresh], _photos.Remote);
    }

    [Fact]
    public async Task Unchanged_photo_on_form_save_is_kept()
    {
        var photoId = await CaptureAsync();
        await _sut.SetPhotoAsync(await BookAsync(), photoId);

        await _sut.PhotoChangedAsync(previous: photoId, current: photoId);

        Assert.Contains(photoId, _photos.Remote);
    }

    [Fact]
    public async Task Other_devices_photo_is_downloaded_when_shown()
    {
        _photos.Remote.Add("fromwife123456");

        Assert.Equal("blob:fromwife123456", await _sut.GetUrlAsync("fromwife123456"));
        Assert.Equal(["fromwife123456"], _photos.Downloads);
    }

    [Fact]
    public async Task Nothing_uploads_without_a_library()
    {
        _session.SetLibrary(null);
        var photoId = await CaptureAsync();

        await _sut.SetPhotoAsync(await BookAsync(), photoId);

        Assert.Empty(_photos.Remote);
        Assert.Equal(1, _sut.PendingCount);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "Condition was not met in time.");
    }
}

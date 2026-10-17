using Bookshelf.Services.Firebase;
using Bookshelf.Tests.Fakes;

namespace Bookshelf.Tests.Sync;

public class FirestoreRemoteStoreTests
{
    private readonly FakeFirebaseClient _client = new();
    private readonly RemoteSession _session = new();
    private readonly FirestoreRemoteStore _sut;

    public FirestoreRemoteStoreTests() => _sut = new FirestoreRemoteStore(_client, _session);

    [Fact]
    public void Is_configured_only_with_a_library_and_raises_changes()
    {
        var raised = 0;
        _sut.ConfigurationChanged += () => raised++;
        Assert.False(_sut.IsConfigured);

        _session.SetLibrary("lib1");
        _session.SetLibrary("lib1");
        Assert.True(_sut.IsConfigured);

        _session.SetLibrary(null);
        Assert.False(_sut.IsConfigured);
        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task First_pull_reads_everything_and_returns_newest_server_time_as_cursor()
    {
        _session.SetLibrary("lib1");
        _client.Books["lib1"] = [new(TestBooks.Make("a"), 5_000_000), new(TestBooks.Make("b"), 7_000_000)];

        var result = await _sut.GetChangesAsync(null);

        Assert.Equal([null], _client.ChangeQueries);
        Assert.Equal(["a", "b"], result.Books.Select(b => b.Id));
        Assert.Equal("7000000;", result.Cursor);
    }

    [Fact]
    public async Task Later_pulls_overlap_the_cursor_to_catch_late_commits()
    {
        _session.SetLibrary("lib1");

        await _sut.GetChangesAsync("7000000;9000000");

        Assert.Equal([7_000_000 - FirestoreRemoteStore.OverlapMillis], _client.ChangeQueries);
        Assert.Equal([9_000_000 - FirestoreRemoteStore.OverlapMillis], _client.ReadingChangeQueries);
    }

    [Fact]
    public async Task Cursor_never_moves_backwards()
    {
        _session.SetLibrary("lib1");
        // Only an older doc inside the overlap window comes back.
        _client.Books["lib1"] = [new(TestBooks.Make("a"), 6_950_000)];

        var result = await _sut.GetChangesAsync("7000000;");

        Assert.Equal("7000000;", result.Cursor);
    }

    [Fact]
    public async Task Empty_pull_keeps_cursor()
    {
        _session.SetLibrary("lib1");

        Assert.Null((await _sut.GetChangesAsync(null)).Cursor);
        Assert.Equal("123;456", (await _sut.GetChangesAsync("123;456")).Cursor);
    }

    [Fact]
    public async Task Readings_have_their_own_cursor()
    {
        _session.SetLibrary("lib1");
        _client.Books["lib1"] = [new(TestBooks.Make("a"), 5_000_000)];
        _client.Readings["lib1"] = [new(TestReadings.Make("a", "chris", rating: 4), 8_000_000)];

        var result = await _sut.GetChangesAsync(null);

        Assert.Equal(["a_chris"], result.Readings.Select(r => r.Id));
        Assert.Equal("5000000;8000000", result.Cursor);
    }

    [Fact]
    public async Task Cursor_from_before_readings_pulls_all_readings()
    {
        _session.SetLibrary("lib1");

        await _sut.GetChangesAsync("7000000");

        Assert.Equal([7_000_000 - FirestoreRemoteStore.OverlapMillis], _client.ChangeQueries);
        Assert.Equal([null], _client.ReadingChangeQueries);
    }

    [Fact]
    public async Task Push_writes_readings_to_current_library()
    {
        _session.SetLibrary("lib1");

        await _sut.PutReadingsAsync([TestReadings.Make("a", "chris")]);

        Assert.Single(_client.Readings["lib1"]);
    }

    [Fact]
    public async Task Push_writes_to_current_library()
    {
        _session.SetLibrary("lib1");

        await _sut.PutBooksAsync([TestBooks.Make("a")]);

        Assert.Single(_client.Books["lib1"]);
    }

    [Fact]
    public async Task Throws_without_a_library() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.GetChangesAsync(null));
}

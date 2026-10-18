using System.Net;

namespace Bookshelf.Tests.Fakes;

/// <summary>Answers HTTP requests from a list of (URL fragment → response) rules and records requests.</summary>
public sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly List<(string Fragment, Func<HttpResponseMessage> Respond)> _rules = [];

    public List<string> Requests { get; } = [];

    public StubHttpHandler Json(string urlFragment, string fixture) =>
        On(urlFragment, () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Lookup", "Fixtures", fixture)),
                System.Text.Encoding.UTF8, "application/json"),
        });

    /// <summary>Answers with a status code and a fixture body (e.g. an API's error JSON).</summary>
    public StubHttpHandler On(string urlFragment, HttpStatusCode status, string fixture) =>
        On(urlFragment, () => new HttpResponseMessage(status)
        {
            Content = new StringContent(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Lookup", "Fixtures", fixture)),
                System.Text.Encoding.UTF8, "application/json"),
        });

    public StubHttpHandler Status(string urlFragment, HttpStatusCode status) =>
        On(urlFragment, () => new HttpResponseMessage(status) { Content = new StringContent("{}") });

    public StubHttpHandler Throws(string urlFragment) =>
        On(urlFragment, () => throw new HttpRequestException("Network unreachable"));

    private StubHttpHandler On(string urlFragment, Func<HttpResponseMessage> respond)
    {
        _rules.Add((urlFragment, respond));
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.AbsoluteUri;
        Requests.Add(url);
        var rule = _rules.FirstOrDefault(r => url.Contains(r.Fragment));
        return rule.Respond is null
            ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))
            : Task.FromResult(rule.Respond());
    }
}

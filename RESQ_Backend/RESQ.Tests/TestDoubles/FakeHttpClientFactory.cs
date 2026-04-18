using System.Text.Json;

namespace RESQ.Tests.TestDoubles;

internal sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler = handler;

    public HttpClient CreateClient(string name)
    {
        return new HttpClient(_handler, disposeHandler: false);
    }
}

internal sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory = responseFactory;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastBody = request.Content == null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        return _responseFactory(request);
    }

    public JsonElement GetLastJsonBody()
    {
        if (string.IsNullOrWhiteSpace(LastBody))
        {
            throw new InvalidOperationException("No request body was captured.");
        }

        using var document = JsonDocument.Parse(LastBody);
        return document.RootElement.Clone();
    }
}

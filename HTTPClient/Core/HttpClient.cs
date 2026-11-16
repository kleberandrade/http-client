using System.Net;
using Transport = System.Net.Http.HttpClient;

namespace HttpClient.Core;

/// <summary>Executes requests using the platform's HTTP and TLS implementation.</summary>
public class HttpClient
{
    private static readonly Transport SharedTransport = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    }) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

    private readonly Request request;
    private readonly Transport transport;

    /// <summary>Overall deadline, including reading the response body. Defaults to 100 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    public HttpClient(Request request) : this(request, SharedTransport) { }

    /// <summary>The caller retains ownership of an injected transport and its configuration.</summary>
    public HttpClient(Request request, Transport transport)
    {
        this.request = request ?? throw new ArgumentNullException(nameof(request));
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public Response Request() => RequestAsync().GetAwaiter().GetResult();

    public async Task<Response> RequestAsync(CancellationToken cancellationToken = default)
    {
        if (Timeout <= TimeSpan.Zero && Timeout != System.Threading.Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(Timeout));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Timeout);
        using var message = request.CreateMessage();
        using var response = await transport.SendAsync(message, deadline.Token).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = new Response($"HTTP/{response.Version}", (int)response.StatusCode, response.ReasonPhrase ?? "")
        {
            Body = body
        };
        foreach (var header in response.Headers.Concat(response.Content.Headers))
            foreach (var value in header.Value)
                result.AddHeader(header.Key, value);
        return result;
    }
}

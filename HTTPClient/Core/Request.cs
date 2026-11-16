using System.Net.Http;
using System.Text;

namespace HttpClient.Core;

/// <summary>A mutable HTTP request with query parameters and a URL-encoded form.</summary>
public class Request
{
    private readonly Dictionary<string, string> getParameters = new();
    private readonly Dictionary<string, string> postParameters = new();
    private readonly Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);

    public Uri Uri { get; set; }
    public string Method { get; set; }
    public string Version { get; set; }
    public IReadOnlyDictionary<string, string> Headers => headers;

    public Request(string url) : this(url, HTTPMethod.GET) { }
    public Request(string url, string method) : this(url, method, HTTPVersion.HTTP11) { }
    public Request(string url, string method, string version)
    {
        Uri = new Uri(url, UriKind.Absolute);
        Method = method;
        Version = version;
        Validate();
    }

    public void AddHeader(string header, string value)
    {
        ValidateHeader(header, value);
        headers.Add(header, value);
    }

    public void ClearHeaders() => headers.Clear();
    public void AddGetParameter(string parameter, string value) => getParameters.Add(parameter, value);
    public void ClearGetParameters() => getParameters.Clear();
    public void AddPostParameter(string parameter, string value) => postParameters.Add(parameter, value);
    public void ClearPostParameters() => postParameters.Clear();

    internal HttpRequestMessage CreateMessage()
    {
        Validate();
        var message = new HttpRequestMessage(new HttpMethod(Method), BuildUri())
        {
            Version = Version == HTTPVersion.HTTP10 ? new Version(1, 0) : new Version(1, 1)
        };
        try
        {
            if (postParameters.Count > 0)
                message.Content = new FormUrlEncodedContent(postParameters);
            foreach (var header in headers)
            {
                // Framing belongs to the transport, not to caller-supplied text.
                if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Content-Length and Transfer-Encoding are managed by the transport.");
                if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                {
                    message.Content ??= new ByteArrayContent(Array.Empty<byte>());
                    message.Content.Headers.Remove(header.Key);
                    message.Content.Headers.Add(header.Key, header.Value);
                }
                else
                    message.Headers.Add(header.Key, header.Value);
            }
            return message;
        }
        catch
        {
            message.Dispose();
            throw;
        }
    }

    private Uri BuildUri()
    {
        var builder = new UriBuilder(Uri) { Fragment = "" };
        var query = builder.Query.TrimStart('?');
        var extra = Encode(getParameters);
        builder.Query = query.Length == 0 ? extra : query + (extra.Length == 0 ? "" : "&" + extra);
        return builder.Uri;
    }

    private static string Encode(Dictionary<string, string> parameters) =>
        string.Join("&", parameters.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value)));

    private void Validate()
    {
        if (Uri is null || !Uri.IsAbsoluteUri || (Uri.Scheme != "http" && Uri.Scheme != "https"))
            throw new ArgumentException("An absolute HTTP or HTTPS URL is required.", nameof(Uri));
        if (Uri.UserInfo.Length != 0)
            throw new ArgumentException("Use an Authorization header instead of URL credentials.", nameof(Uri));
        if (!IsToken(Method)) throw new ArgumentException("Invalid HTTP method.", nameof(Method));
        if (Version != HTTPVersion.HTTP10 && Version != HTTPVersion.HTTP11)
            throw new ArgumentException("Only HTTP/1.0 and HTTP/1.1 are supported.", nameof(Version));
    }

    internal static void ValidateHeader(string name, string value)
    {
        if (!IsToken(name)) throw new ArgumentException("Invalid HTTP header name.", nameof(name));
        if (value is null || value.Any(c => c == '\r' || c == '\n' || c == '\0' || (c < ' ' && c != '\t') || c == 127))
            throw new ArgumentException("Invalid HTTP header value.", nameof(value));
    }

    private static bool IsToken(string? value) => !string.IsNullOrEmpty(value) &&
        value.All(c => c >= '0' && c <= '9' || c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || "!#$%&'*+-.^_`|~".Contains(c));

    /// <summary>Returns an HTTP/1.x diagnostic representation; the transport sends the request.</summary>
    public override string ToString()
    {
        using var message = CreateMessage();
        var output = new StringBuilder($"{Method} {message.RequestUri!.PathAndQuery} {Version}\r\n");
        if (!headers.ContainsKey("Host")) output.Append($"Host: {message.RequestUri.Authority}\r\n");
        foreach (var header in message.Headers)
            output.Append($"{header.Key}: {string.Join(", ", header.Value)}\r\n");
        if (message.Content is not null)
        {
            var body = message.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            foreach (var header in message.Content.Headers)
                if (!header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                    output.Append($"{header.Key}: {string.Join(", ", header.Value)}\r\n");
            output.Append($"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}");
        }
        else output.Append("\r\n");
        return output.ToString();
    }
}

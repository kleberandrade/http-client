using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using HttpClient.Core;
using Xunit;
using Client = HttpClient.Core.HttpClient;

namespace HTTPClient.Tests;

public class TransportTests
{
    [Fact]
    public async Task ReadsChunkedResponseWithoutWaitingForConnectionClose()
    {
        using var server = new LocalServer();
        using var release = new CancellationTokenSource();
        var exchange = server.ExchangeAsync(async stream =>
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Type: text/plain; charset=utf-8\r\nSet-Cookie: a=1\r\nSet-Cookie: b=2\r\n\r\n5\r\nhello\r\n6\r\n world\r\n0\r\n\r\n"));
            try { await Task.Delay(Timeout.InfiniteTimeSpan, release.Token); }
            catch (OperationCanceledException) { }
        });
        try
        {
            var response = await new Client(new Request(server.Url)) { Timeout = TimeSpan.FromSeconds(5) }.RequestAsync();
            Assert.Equal("hello world", response.Body);
            Assert.Equal(2, response.Headers["set-cookie"].Count);
        }
        finally { release.Cancel(); await exchange; }
    }

    [Fact]
    public async Task SendsEncodedPostAndReadsGzipUtf8ErrorBody()
    {
        using var server = new LocalServer();
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
            gzip.Write(Encoding.UTF8.GetBytes("Não encontrado"));
        var bytes = compressed.ToArray();
        var exchange = server.ExchangeAsync(async stream =>
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 404 Not Found\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Encoding: gzip\r\nContent-Length: {bytes.Length}\r\n\r\n"));
            await stream.WriteAsync(bytes);
        });
        var request = new Request(server.Url + "?old=1", HTTPMethod.POST);
        request.AddGetParameter("q", "a&b");
        request.AddPostParameter("name", "João & Ana");
        var response = await new Client(request).RequestAsync();
        await exchange;
        Assert.Equal(404, response.Code);
        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal("Não encontrado", response.Body);
        Assert.StartsWith("POST /?old=1&q=a%26b HTTP/1.1", server.RequestText);
        Assert.Contains($"Host: 127.0.0.1:{server.Port}\r\n", server.RequestText);
        Assert.Contains("Content-Type: application/x-www-form-urlencoded", server.RequestText);
        Assert.Equal("name=Jo%C3%A3o+%26+Ana", server.Body);
    }

    [Theory]
    [InlineData(HTTPMethod.HEAD, 200)]
    [InlineData(HTTPMethod.GET, 204)]
    public async Task HandlesResponsesWithNoBody(string method, int code)
    {
        using var server = new LocalServer();
        var exchange = server.ExchangeAsync(stream => stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {code} OK\r\nContent-Length: 0\r\n\r\n")).AsTask());
        var response = await new Client(new Request(server.Url, method)).RequestAsync();
        await exchange;
        Assert.Equal(code, response.Code);
        Assert.Empty(response.Body);
    }

    [Fact]
    public async Task ReturnsRedirectWithoutFollowingIt()
    {
        using var server = new LocalServer();
        var exchange = server.ExchangeAsync(stream => stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:1/never\r\nContent-Length: 0\r\n\r\n")).AsTask());
        var response = await new Client(new Request(server.Url)).RequestAsync();
        await exchange;
        Assert.Equal(302, response.Code);
        Assert.Equal("http://127.0.0.1:1/never", Assert.Single(response.Headers["Location"]));
    }

    [Fact]
    public async Task SyncApiStillWorks()
    {
        using var server = new LocalServer();
        var exchange = server.ExchangeAsync(stream => stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok")).AsTask());
        var response = new Client(new Request(server.Url)).Request();
        await exchange;
        Assert.Equal("ok", response.Body);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CallerCancellationAndDeadlineInterruptBodyRead(bool callerCancellation)
    {
        using var server = new LocalServer();
        using var release = new CancellationTokenSource();
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exchange = server.ExchangeAsync(async stream =>
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 100\r\n\r\npartial"));
            received.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, release.Token); }
            catch (OperationCanceledException) { }
        });
        using var cancellation = new CancellationTokenSource();
        var client = new Client(new Request(server.Url))
        {
            Timeout = callerCancellation ? TimeSpan.FromSeconds(10) : TimeSpan.FromMilliseconds(500)
        };
        try
        {
            var pending = client.RequestAsync(cancellation.Token);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (callerCancellation) cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }
        finally { release.Cancel(); await exchange; }
    }

    [Fact]
    public async Task NetworkFailureIsNotTurnedIntoNull()
    {
        using var server = new LocalServer();
        var url = server.Url;
        server.Dispose();
        await Assert.ThrowsAsync<HttpRequestException>(() => new Client(new Request(url)).RequestAsync());
    }

    private sealed class LocalServer : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        public int Port { get; }
        public string Url => $"http://127.0.0.1:{Port}/";
        public string RequestText { get; private set; } = "";
        public string Body { get; private set; } = "";

        public LocalServer()
        {
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        public async Task ExchangeAsync(Func<NetworkStream, Task> respond)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var connection = await listener.AcceptTcpClientAsync(deadline.Token);
            using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            var text = new StringBuilder();
            int length = 0;
            while (await reader.ReadLineAsync(deadline.Token) is { Length: > 0 } line)
            {
                text.Append(line).Append("\r\n");
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    length = int.Parse(line.Split(':')[1]);
            }
            RequestText = text.ToString();
            var buffer = new char[length];
            int offset = 0;
            while (offset < length)
            {
                int read = await reader.ReadAsync(buffer.AsMemory(offset), deadline.Token);
                if (read == 0) throw new EndOfStreamException();
                offset += read;
            }
            Body = new string(buffer);
            await respond(stream);
        }

        public void Dispose() => listener.Stop();
    }
}

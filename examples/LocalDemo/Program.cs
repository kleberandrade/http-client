using System.Net;
using System.Net.Sockets;
using System.Text;
using HttpClient.Core;
using Client = HttpClient.Core.HttpClient;

// A self-contained example: no public service, fixed port, or credentials required.
using var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
var endpoint = (IPEndPoint)listener.LocalEndpoint;
var server = RespondAsync(listener);
var request = new Request($"http://127.0.0.1:{endpoint.Port}/hello?source=demo");
request.AddGetParameter("name", "Kleber Andrade");
request.AddHeader("Accept", "text/plain");
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var response = await new Client(request).RequestAsync(deadline.Token);
await server;
Console.WriteLine($"{response.Code} {response.Message}\n{response.Body}");
if (response.Code != 200 || response.Body != "Hello, Kleber Andrade!")
    throw new InvalidOperationException("Unexpected demo response.");

static async Task RespondAsync(TcpListener listener)
{
    using var connection = await listener.AcceptTcpClientAsync();
    using var stream = connection.GetStream();
    using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
    var line = await reader.ReadLineAsync();
    if (line != "GET /hello?source=demo&name=Kleber%20Andrade HTTP/1.1")
        throw new InvalidOperationException($"Unexpected request: {line}");
    while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) { }
    var body = Encoding.UTF8.GetBytes("Hello, Kleber Andrade!");
    await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n"));
    await stream.WriteAsync(body);
}

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using HttpClient.Core;
using Xunit;
using Client = HttpClient.Core.HttpClient;

namespace HTTPClient.Tests;

public class TlsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpsValidatesCertificatesAndSupportsCallerOwnedTransport(bool trustTestCertificate)
    {
        using var key = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serving = ServeAsync();
        var request = new Request($"https://localhost:{port}/");
        using var handler = new HttpClientHandler { UseProxy = false };
        if (trustTestCertificate)
            handler.ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.Thumbprint == certificate.Thumbprint;
        using var transport = new System.Net.Http.HttpClient(handler);
        var client = trustTestCertificate ? new Client(request, transport) : new Client(request);
        try
        {
            if (trustTestCertificate)
            {
                Assert.Equal("tls", (await client.RequestAsync(deadline.Token)).Body);
                // The wrapper must not dispose the caller's transport after sending.
                transport.DefaultRequestHeaders.Add("X-Still-Usable", "yes");
            }
            else
                await Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(() => client.RequestAsync(deadline.Token));
        }
        finally { await serving; }

        async Task ServeAsync()
        {
            using var connection = await listener.AcceptTcpClientAsync(deadline.Token);
            using var ssl = new SslStream(connection.GetStream());
            try
            {
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, deadline.Token);
                if (!trustTestCertificate) return;
                using var reader = new StreamReader(ssl, Encoding.ASCII, false, 1024, leaveOpen: true);
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync(deadline.Token))) { }
                await ssl.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\ntls"), deadline.Token);
            }
            catch (Exception ex) when (!trustTestCertificate && ex is AuthenticationException or IOException) { }
        }
    }
}

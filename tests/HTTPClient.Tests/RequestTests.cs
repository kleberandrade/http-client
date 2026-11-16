using HttpClient.Core;
using Xunit;

namespace HTTPClient.Tests;

public class RequestTests
{
    [Fact]
    public void QueryPreservesExistingValuesAndEscapesNewParameters()
    {
        var request = new Request("https://example.com:8443/search?existing=1#ignored");
        request.AddGetParameter("a b", "c&d=é");
        var text = request.ToString();
        Assert.StartsWith("GET /search?existing=1&a%20b=c%26d%3D%C3%A9 HTTP/1.1\r\n", text);
        Assert.Contains("Host: example.com:8443\r\n", text);
        Assert.DoesNotContain("ignored", text);
    }

    [Fact]
    public void FormUsesEncodedBodyAndByteLength()
    {
        var request = new Request("http://example.com", HTTPMethod.POST);
        request.AddPostParameter("name", "João & Ana");
        var text = request.ToString();
        Assert.Contains("Content-Type: application/x-www-form-urlencoded\r\n", text);
        Assert.EndsWith("\r\n\r\nname=Jo%C3%A3o+%26+Ana", text);
        Assert.Contains("Content-Length: 22\r\n", text);
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com")]
    [InlineData("https://user:pass@example.com")]
    public void RejectsUnsupportedUrls(string url) => Assert.Throws<ArgumentException>(() => new Request(url));

    [Theory]
    [InlineData("Bad Header", "value")]
    [InlineData("X-Test", "ok\r\nInjected: true")]
    [InlineData("X-Test", "bad\0")]
    public void RejectsHeaderInjection(string name, string value) =>
        Assert.Throws<ArgumentException>(() => new Request("https://example.com").AddHeader(name, value));

    [Fact]
    public void ClearMethodsRemoveOnlyTheirOwnValues()
    {
        var request = new Request("https://example.com");
        request.AddGetParameter("q", "one");
        request.AddPostParameter("f", "two");
        request.AddHeader("X-Test", "three");
        request.ClearGetParameters();
        request.ClearPostParameters();
        request.ClearHeaders();
        Assert.Equal("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", request.ToString());
    }

    [Fact]
    public void RevalidatesMutablePropertiesAndRejectsManualFraming()
    {
        var request = new Request("http://example.com") { Method = "GET\r\nInjected" };
        Assert.Throws<ArgumentException>(() => request.ToString());
        request.Method = HTTPMethod.GET;
        request.Version = "HTTP/9";
        Assert.Throws<ArgumentException>(() => request.ToString());
        request.Version = HTTPVersion.HTTP11;
        request.AddHeader("Content-Length", "100");
        Assert.Throws<ArgumentException>(() => request.ToString());
    }
}

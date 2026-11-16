using HttpClient.Core;
using Xunit;

namespace HTTPClient.Tests;

public class ResponseTests
{
    [Fact]
    public void ParsesRepeatedHeadersColonsAndBlankLinesInBody()
    {
        const string raw = "HTTP/1.1 200 All Good\r\nLocation:https://example.com:8443/a\r\nSet-Cookie: a=1\r\nset-cookie: b=2\r\n\r\nfirst\r\n\r\nlast";
        var response = new Response(raw);
        Assert.Equal(200, response.Code);
        Assert.Equal("All Good", response.Message);
        Assert.Equal("https://example.com:8443/a", Assert.Single(response.Headers["location"]));
        Assert.Equal(new[] { "a=1", "b=2" }, response.Headers["SET-COOKIE"]);
        Assert.Equal("first\r\n\r\nlast", response.Body);
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(response.Body, new Response(response.ToString()).Body);
    }

    [Fact]
    public void AcceptsEmptyBodyAndMissingReasonPhrase()
    {
        var response = new Response("HTTP/1.0 204\r\n\r\n");
        Assert.Equal("", response.Message);
        Assert.Equal("", response.Body);
        response.AddHeader("X-Test", "one");
        response.ClearHeaders();
        Assert.Empty(response.Headers);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("HTTP/1.1 invalid OK\r\n\r\n")]
    [InlineData("HTTP/1.1 999 Wrong\r\n\r\n")]
    [InlineData("HTTP/1.1 200 OK\r\nMissingColon\r\n\r\n")]
    public void MalformedResponsesHaveClearErrors(string raw) => Assert.Throws<FormatException>(() => new Response(raw));
}

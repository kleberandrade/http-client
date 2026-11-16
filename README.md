# HTTP Client

[![C# 14](https://img.shields.io/badge/C%23-14-512BD4)](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![.NET Standard 2.1](https://img.shields.io/badge/.NET_Standard-2.1-512BD4)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![CI](https://github.com/kleberandrade/http-client/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/kleberandrade/http-client/actions/workflows/ci.yml?query=branch%3Amain)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/kleberandrade/http-client/blob/main/LICENSE)

A small C# library for consuming HTTP web services. Originally written in 2014 for
Unity mobile projects, it now uses `System.Net.Http` for connection pooling, TLS,
HTTP framing, and decompression while keeping the `HttpClient.Core` request/response API.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), supported LTS
  ([Microsoft support policy](https://dotnet.microsoft.com/platform/support/policy)).
  `global.json` selects a stable .NET 10 SDK and allows newer feature bands.
- Windows, macOS, or Linux for development. Tests and the example target .NET 10.
- The library builds for **net10.0** and **netstandard2.1**, with no third-party runtime dependencies.

The .NET Standard assembly is intended for compatible hosts, including Unity configurations
that support .NET Standard 2.1. Unity Editor, IL2CPP, mobile devices, and WebGL have **not**
been validated. The project no longer targets .NET Framework 4.5.

## Install and build

```sh
git clone https://github.com/kleberandrade/http-client.git
cd http-client
dotnet restore HTTPClient.sln --locked-mode
dotnet build HTTPClient.sln -c Release --no-restore
```

Reference the source project from a .NET application:

```sh
dotnet add path/to/YourApp.csproj reference HTTPClient/HTTPClient.csproj
```

For a compatible Unity project, build the library and import
`HTTPClient/bin/Release/netstandard2.1/HTTPClient.dll` into `Assets/Plugins`.
Check the host's API compatibility and networking support before deployment.
Import the compiled DLL rather than these C# 14 sources into older Unity compilers.

## Quick start

Run the self-contained example; it starts an HTTP server on a temporary loopback
port, sends a request, checks the result, and exits. No internet service is needed.

```sh
dotnet run --project examples/LocalDemo -c Release
```

Expected output:

```text
200 OK
Hello, Kleber Andrade!
```

## API and usage

```csharp
using HttpClient.Core;
using Client = HttpClient.Core.HttpClient;

var request = new Request("https://example.com/search?lang=en");
request.AddGetParameter("q", "Unity & C#");
request.AddHeader("Accept", "application/json");

var client = new Client(request) { Timeout = TimeSpan.FromSeconds(15) };
using var cancellation = new CancellationTokenSource();
var response = await client.RequestAsync(cancellation.Token);
Console.WriteLine($"{response.Code}: {response.Body}");
if (response.Headers.TryGetValue("Content-Type", out var values))
    Console.WriteLine(string.Join(", ", values));
```

The `Client` alias avoids ambiguity with `System.Net.Http.HttpClient`.
The URL above is illustrative; the loopback example is the executable quick start.

### Requests

- `Request(url, method = GET, version = HTTP/1.1)` is available as three constructor overloads.
  Only absolute HTTP/HTTPS URLs and HTTP/1.0 or HTTP/1.1 are accepted. Fragments are
  omitted; credentials in URLs are rejected. Use an Authorization header instead.
- `AddGetParameter(name, value)` escapes names and values and appends them to the
  existing query. `ClearGetParameters()` clears only parameters added through this API.
- `AddPostParameter(name, value)` adds a URL-encoded form field. Fields generate a
  request body even when another method is selected; use POST, PUT, or PATCH as appropriate.
  Values use UTF-8 form encoding and the transport supplies the byte length.
- `AddHeader(name, value)` rejects invalid names, control characters, and CRLF injection.
  Header names are case insensitive. Duplicate request names or parameter names throw
  `ArgumentException`; clear the relevant collection before replacing values.
  `Content-Length` and `Transfer-Encoding` are managed by the transport and rejected at send time.
- `ClearHeaders()` and `ClearPostParameters()` reset their respective collections.
  The transport generates Host automatically, including a non-default port.
- `Method`, `Uri`, and `Version` remain mutable and are revalidated before sending.
  `HTTPMethod` includes GET, POST, PUT, DELETE, HEAD, OPTIONS, TRACE, CONNECT, and PATCH;
  token-valid custom methods are accepted, subject to platform transport rules.

```csharp
var request = new Request("https://example.com/forms", HTTPMethod.POST);
request.AddPostParameter("name", "João & Ana");
request.AddPostParameter("message", "Hello world");
var response = await new Client(request).RequestAsync();
```

### Execution and responses

- `RequestAsync(CancellationToken)` is preferred. `Request()` preserves the synchronous
  entry point and blocks the calling thread; avoid it on a UI/game thread.
- `Timeout` defaults to 100 seconds and covers receiving the complete body.
  Use `System.Threading.Timeout.InfiniteTimeSpan` to disable it. Timeout/cancellation
  raises `OperationCanceledException`; network/TLS failures raise `HttpRequestException`.
- HTTP error statuses such as 404 return a `Response`, including its body.
  Inspect `Code`, `Message`, `Version`, `Body`, and `IsSuccessStatusCode`.
- `Headers` is case insensitive and preserves repeated response values, including
  separate `Set-Cookie` values. `AddHeader()` appends a response value; `ClearHeaders()` clears them.
- The shared default transport pools connections, validates HTTPS certificates using
  the platform, decodes gzip/deflate, and disables redirects and automatic cookies.
  Follow redirects explicitly if needed; a 3xx response is returned to the caller.
- `new Client(request, systemHttpClient)` uses a caller-owned `System.Net.Http.HttpClient`.
  It is never disposed by this library. Its handler controls redirects, certificates,
  cookies, proxies, and decompression; its timeout can impose a shorter deadline.
  Keep injected transports long lived. No certificate validation bypass is enabled by this library.

### Diagnostic text and compatibility

`Request.ToString()` produces HTTP/1.x diagnostic text with escaped parameters and form
headers. `Response.ToString()` prints decoded response data. These are not wire serializers:
a response may retain framing or length headers that no longer describe its decoded text.

`new Response(rawText)` parses a **complete, already decoded** HTTP/1.x status line,
headers, and body. It handles repeated headers, colons in values, empty bodies, and
blank lines inside the body. Malformed input raises `FormatException`. It does not decode
chunk framing or compression; live requests delegate those operations to the transport.

Migration from 1.x: the namespace and constructors are retained; method/version constants
are now immutable. Invalid input fails explicitly. Headers are readable, request names are
case insensitive, query/form encoding is corrected, and errors no longer return `null`.
Projects targeting .NET Framework 4.5 must move to a compatible supported host.

## Tests and local package

```sh
dotnet test HTTPClient.sln -c Release
dotnet run --project examples/LocalDemo -c Release
dotnet pack HTTPClient/HTTPClient.csproj -c Release
```

The xUnit suite covers encoding, header injection, response parsing, repeated headers,
chunked keep-alive responses, gzip/UTF-8, empty/HEAD responses, HTTP errors, redirects,
network failures, sync execution, cancellation during body reads, deadlines, HTTPS
certificate rejection, and TLS with a caller-owned transport. Network tests use ephemeral
loopback servers and temporary certificates without modifying the system trust store.
Validated locally on macOS arm64 with SDK 10.0.404: both target builds, 27 tests,
the loopback example, and local package creation passed. The NuGet vulnerability
check reported no known vulnerable direct or transitive dependencies. Unity and
other operating systems were not run locally.

NuGet test dependencies are pinned with a committed lock file. Package creation is local;
nothing is published by these commands.

GitHub Actions runs restore, both target builds, tests, the example, and package creation
on Linux, Windows, and macOS. A local run validates only its host platform. Check the
[CI runs](https://github.com/kleberandrade/http-client/actions/workflows/ci.yml)
for the results of the cross-platform matrix on each commit.

## Limitations

- Responses are fully buffered and decoded as text. There is no streaming, binary download,
  JSON object mapping, multipart upload, retry policy, or built-in authentication flow.
  Use `System.Net.Http` directly for those features. Large bodies consume memory; an injected
  transport can lower `MaxResponseContentBufferSize`.
- The wrapper exposes HTTP/1.0 and HTTP/1.1 only, and does not expose response trailers.
- Request objects are mutable and must not be changed during a send or shared between
  concurrent writers. Configure a request before calling the client.
- TLS/platform behavior, proxies, and Unity/mobile networking depend on the runtime.
  Loopback tests do not validate production service interoperability or device behavior.

## License

[MIT](LICENSE). Copyright © 2014 Kléber de Oliveira Andrade and © 2026 Kleber Andrade.

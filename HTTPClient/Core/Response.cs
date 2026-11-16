using System.Globalization;
using System.Text;

namespace HttpClient.Core;

/// <summary>A buffered text response. Repeated headers retain their individual values.</summary>
public class Response
{
    private readonly Dictionary<string, List<string>> headers = new(StringComparer.OrdinalIgnoreCase);
    public string Version { get; set; }
    public int Code { get; set; }
    public string Message { get; set; }
    public string Body { get; set; } = "";
    public bool IsSuccessStatusCode => Code >= 200 && Code <= 299;
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers =>
        headers.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value.AsReadOnly(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Parses a complete, already decoded HTTP/1.x text response, not wire framing.</summary>
    public Response(string response)
    {
        if (response is null) throw new ArgumentNullException(nameof(response));
        int separator = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (separator < 0) throw new FormatException("Missing HTTP header terminator.");
        var lines = response.Substring(0, separator).Split(new[] { "\r\n" }, StringSplitOptions.None);
        var status = lines[0].Split(new[] { ' ' }, 3);
        if (status.Length < 2 || (status[0] != HTTPVersion.HTTP10 && status[0] != HTTPVersion.HTTP11) ||
            status[1].Length != 3 || !int.TryParse(status[1], NumberStyles.None, CultureInfo.InvariantCulture, out int code) || code < 100 || code > 599)
            throw new FormatException("Invalid HTTP status line.");
        Version = status[0];
        Code = code;
        Message = status.Length == 3 ? status[2] : "";
        for (int i = 1; i < lines.Length; i++)
        {
            int colon = lines[i].IndexOf(':');
            if (colon <= 0) throw new FormatException("Invalid HTTP header.");
            try { AddHeader(lines[i].Substring(0, colon), lines[i].Substring(colon + 1).Trim(' ', '\t')); }
            catch (ArgumentException ex) { throw new FormatException("Invalid HTTP header.", ex); }
        }
        Body = response.Substring(separator + 4);
    }

    public Response(string version, int code, string message)
    {
        Version = version;
        Code = code;
        Message = message;
    }

    public void AddHeader(string header, string value)
    {
        Request.ValidateHeader(header, value);
        if (!headers.TryGetValue(header, out var values)) headers.Add(header, values = new List<string>());
        values.Add(value);
    }

    public void ClearHeaders() => headers.Clear();

    /// <summary>Returns decoded diagnostic text; this is not a wire-format serializer.</summary>
    public override string ToString()
    {
        var output = new StringBuilder($"{Version} {Code} {Message}\r\n");
        foreach (var header in headers)
            foreach (var value in header.Value)
                output.Append($"{header.Key}: {value}\r\n");
        return output.Append("\r\n").Append(Body).ToString();
    }
}

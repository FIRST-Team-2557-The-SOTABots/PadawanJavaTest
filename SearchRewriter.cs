using System.Text.RegularExpressions;

namespace FrcResearchTest;

/// <summary>
/// Forces Google searches onto the Web-only result page (udm=14), which is the
/// text-link view with no AI Overview. Also rewrites AI Mode links (udm=50).
/// </summary>
public static class SearchRewriter
{
    private static readonly Regex GoogleHostPattern =
        new(@"^google\.[a-z]{2,3}(\.[a-z]{2})?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AiModeParam =
        new(@"(?<pre>[?&])udm=50(?<post>[&#]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryRewrite(string url, out string rewritten, out string reason)
    {
        rewritten = url;
        reason = "";

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (!IsGoogleHost(uri.Host)) return false;
        if (!uri.AbsolutePath.Equals("/search", StringComparison.OrdinalIgnoreCase)) return false;

        var query = ParseQuery(uri.Query);

        if (query.TryGetValue("udm", out var udm))
        {
            if (udm == "14" || udm.Equals("web", StringComparison.OrdinalIgnoreCase)) return false;

            if (udm == "50")
            {
                rewritten = AiModeParam.Replace(url, "${pre}udm=14${post}");
                reason = "Google AI Mode redirected to Web results";
                return !string.Equals(rewritten, url, StringComparison.Ordinal);
            }

            // Images, videos, news, shopping and other modes are left alone.
            return false;
        }

        // tbm=isch/shop/vid/nws etc. are not the normal "All" results page.
        if (query.ContainsKey("tbm")) return false;

        rewritten = AppendParameter(url, "udm=14");
        reason = "Google Web results (no AI Overview)";
        return true;
    }

    public static bool TryGetGoogleQuery(string url, out string query)
    {
        query = "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (!IsGoogleHost(uri.Host)) return false;
        if (!uri.AbsolutePath.Equals("/search", StringComparison.OrdinalIgnoreCase)) return false;

        var parameters = ParseQuery(uri.Query);
        if (!parameters.TryGetValue("q", out var q) || string.IsNullOrWhiteSpace(q)) return false;

        query = q;
        return true;
    }

    public static string BuildSearchUrl(string text, string template)
    {
        var encoded = Uri.EscapeDataString(text.Trim());
        return template.Contains("{query}", StringComparison.Ordinal)
            ? template.Replace("{query}", encoded, StringComparison.Ordinal)
            : $"https://www.google.com/search?q={encoded}&udm=14";
    }

    public static bool IsGoogleHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;

        var value = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (value.StartsWith("www.", StringComparison.Ordinal)) value = value[4..];

        return value == "google.com"
               || value.EndsWith(".google.com", StringComparison.Ordinal)
               || GoogleHostPattern.IsMatch(value);
    }

    private static string AppendParameter(string url, string parameter)
    {
        var fragmentIndex = url.IndexOf('#');
        var body = fragmentIndex >= 0 ? url[..fragmentIndex] : url;
        var fragment = fragmentIndex >= 0 ? url[fragmentIndex..] : "";
        var separator = body.Contains('?') ? "&" : "?";
        return body + separator + parameter + fragment;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return result;

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = part.IndexOf('=');
            var key = equals < 0 ? part : part[..equals];
            var value = equals < 0 ? "" : part[(equals + 1)..];
            result[Uri.UnescapeDataString(key.Replace('+', ' '))] =
                Uri.UnescapeDataString(value.Replace('+', ' '));
        }

        return result;
    }
}

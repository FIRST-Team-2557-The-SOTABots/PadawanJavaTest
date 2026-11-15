namespace FrcResearchTest;

public enum CitationStatus
{
    Empty,
    InvalidUrl,
    NotAllowedDomain,
    NotVisited,
    Verified,
}

public sealed class Visit
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTimeOffset At { get; set; }
}

public sealed class VisitLog
{
    private readonly HashSet<string> _keys = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Visit> _visits = new();

    public IReadOnlyList<Visit> Visits => _visits;

    public int UniqueHosts => _visits
        .Select(v => Uri.TryCreate(v.Url, UriKind.Absolute, out var uri)
            ? uri.Host.ToLowerInvariant()
            : "")
        .Where(host => host.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public void Add(string url, string title)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        _keys.Add(Key(url));
        _visits.Add(new Visit
        {
            Url = url,
            Title = title ?? "",
            At = DateTimeOffset.Now,
        });
    }

    public bool Contains(string url) => !string.IsNullOrWhiteSpace(url) && _keys.Contains(Key(url));

    public CitationStatus CheckCitation(string citation, IEnumerable<string> allowedDomains)
    {
        if (string.IsNullOrWhiteSpace(citation)) return CitationStatus.Empty;

        if (!Uri.TryCreate(citation.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return CitationStatus.InvalidUrl;
        }

        if (!IsAllowedDomain(uri.Host, allowedDomains)) return CitationStatus.NotAllowedDomain;

        return Contains(citation) ? CitationStatus.Verified : CitationStatus.NotVisited;
    }

    public static bool IsAllowedDomain(string host, IEnumerable<string> allowedDomains)
    {
        var value = host.Trim().TrimEnd('.').ToLowerInvariant();
        foreach (var entry in allowedDomains)
        {
            var domain = (entry ?? "").Trim().TrimStart('.').TrimEnd('.').ToLowerInvariant();
            if (domain.Length == 0) continue;

            if (value == domain ||
                value.EndsWith("." + domain, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Citation matching ignores scheme, query string, fragment and a trailing
    /// slash, so "#setpoint" anchors and ?utm=... parameters do not break a
    /// legitimate citation.
    /// </summary>
    public static string Key(string url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri))
        {
            return (url ?? "").Trim().ToLowerInvariant();
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        return (uri.Host + path).ToLowerInvariant();
    }
}

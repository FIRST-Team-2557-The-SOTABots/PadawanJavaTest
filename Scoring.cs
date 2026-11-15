namespace FrcResearchTest;

public static class Scoring
{
    /// <summary>
    /// Case-insensitive keyword check. Whitespace is removed before comparing so
    /// "atSetpoint()", "at setpoint", and "AT SETPOINT" all match.
    /// </summary>
    public static bool MatchesKeyword(AcceptSpec? spec, string? answer)
    {
        if (spec is null) return false;

        var text = Squash(answer);
        if (text.Length == 0) return false;

        var all = spec.All ?? new List<string>();
        var any = spec.Any ?? new List<string>();

        if (all.Count == 0 && any.Count == 0) return false;

        foreach (var keyword in all)
        {
            if (!text.Contains(Squash(keyword), StringComparison.Ordinal)) return false;
        }

        if (any.Count > 0 && !any.Any(keyword => text.Contains(Squash(keyword), StringComparison.Ordinal)))
        {
            return false;
        }

        return true;
    }

    private static string Squash(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (!char.IsWhiteSpace(c)) builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }
}

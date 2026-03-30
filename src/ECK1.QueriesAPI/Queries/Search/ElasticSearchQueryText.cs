using System.Text;

namespace ECK1.QueriesAPI.Queries.Search;

internal static class ElasticSearchQueryText
{
    public static bool CanUseWildcard(string q) =>
        !string.IsNullOrWhiteSpace(q) && q.Trim().Length >= 3;

    public static string BuildContainsQueryString(string q)
    {
        // Build a query_string expression that searches for substrings (leading+trailing wildcards)
        // while AND-ing tokens together.
        // Example: "foo bar" -> "*foo* AND *bar*"
        var tokens = Tokenize(q)
            .Where(t => t.Length >= 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (tokens.Length == 0)
            return string.Empty;

        return string.Join(" AND ", tokens.Select(t => $"*{EscapeQueryString(t)}*"));
    }

    private static IEnumerable<string> Tokenize(string q)
    {
        var sb = new StringBuilder();
        foreach (var ch in q)
        {
            if (char.IsLetterOrDigit(ch) || ch == '@' || ch == '.' || ch == '-' || ch == '_' || ch == ':' || ch == '/')
            {
                sb.Append(ch);
            }
            else
            {
                if (sb.Length > 0)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
            }
        }

        if (sb.Length > 0)
            yield return sb.ToString();
    }

    // Escapes reserved characters for query_string syntax (minimal but safe).
    private static string EscapeQueryString(string value)
    {
        // Ref: query_string special chars: + - = && || > < ! ( ) { } [ ] ^ " ~ * ? : \ /
        // We'll escape any non-alnum we didn't already whitelist in Tokenize.
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch == '@' || ch == '.' || ch == '-' || ch == '_' || ch == ':' || ch == '/')
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append('\\').Append(ch);
            }
        }
        return sb.ToString();
    }
}

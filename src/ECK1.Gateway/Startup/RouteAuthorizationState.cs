namespace ECK1.Gateway.Startup;

public class RouteAuthorizationState
{
    private volatile IReadOnlyDictionary<string, List<string>> _rules =
        new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, List<string>> Rules => _rules;

    public void UpdateRules(IReadOnlyDictionary<string, List<string>> rules)
    {
        _rules = rules;
    }

    public List<string> GetRequiredPermissions(string method, string path)
    {
        var key = $"{method}:{path}";
        if (_rules.TryGetValue(key, out var permissions))
            return permissions;

        // Try matching route templates with path segments
        foreach (var (pattern, patternPermissions) in _rules)
        {
            if (RoutePatternMatches(pattern, key))
                return patternPermissions;
        }

        return null;
    }

    private static bool RoutePatternMatches(string pattern, string requestKey)
    {
        var patternParts = pattern.Split('/');
        var requestParts = requestKey.Split('/');

        if (patternParts.Length != requestParts.Length)
            return false;

        for (int i = 0; i < patternParts.Length; i++)
        {
            if (patternParts[i].StartsWith('{') && patternParts[i].EndsWith('}'))
                continue;
            if (!string.Equals(patternParts[i], requestParts[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}

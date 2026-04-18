using System.Net.Http.Headers;

namespace ECK1.TestPlatform.Services;

/// <summary>
/// Holds the Bearer token forwarded from the frontend request.
/// Thread-safe for single-run-at-a-time usage.
/// </summary>
public sealed class BearerTokenStore
{
    private volatile string? _token;

    public string? Token => _token;

    public void SetToken(string? token) => _token = token;

    public void SetAuthorizationHeader(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            _token = null;
            return;
        }

        var token = authorizationHeader.Trim();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            token = token["Bearer ".Length..].Trim();

        _token = string.IsNullOrWhiteSpace(token) ? null : token;
    }
}

/// <summary>
/// Attaches the forwarded Bearer token to outgoing HTTP requests.
/// </summary>
public sealed class ForwardedTokenHandler(BearerTokenStore tokenStore) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = tokenStore.Token;
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return base.SendAsync(request, cancellationToken);
    }
}

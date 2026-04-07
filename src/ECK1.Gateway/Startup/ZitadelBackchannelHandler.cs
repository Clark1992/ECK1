namespace ECK1.Gateway.Startup;

/// <summary>
/// Rewrites HTTP requests from the external Zitadel URL to the internal cluster URL.
/// This allows the JWT middleware to fetch OIDC discovery and JWKS from inside the cluster
/// even though tokens reference the external issuer URL.
/// </summary>
public class ZitadelBackchannelHandler : HttpClientHandler
{
    private readonly string _externalBase;
    private readonly string _internalBase;

    public ZitadelBackchannelHandler(string externalBase, string internalBase)
    {
        _externalBase = externalBase;
        _internalBase = internalBase;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString();
        if (url is not null && url.StartsWith(_externalBase, StringComparison.OrdinalIgnoreCase))
        {
            request.RequestUri = new Uri(url.Replace(_externalBase, _internalBase));
            // Zitadel uses the Host header to identify the instance.
            // After rewriting the URL to the internal address, the Host header
            // must still carry the external domain so Zitadel can resolve it.
            request.Headers.Host = new Uri(_externalBase).Authority;
        }

        return base.SendAsync(request, cancellationToken);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace ECK1.E2E.Tests.API;

/// <summary>
/// Obtains OIDC JWT access tokens for E2E tests via the standard
/// Authorization Code flow with PKCE — the same flow a real browser performs.
/// Uses the Zitadel Sessions v2 API (with a service-account PAT) to
/// programmatically authenticate as a human user.
/// </summary>
public static class ZitadelAuthHelper
{
    private const string RedirectPath = "/signin-oidc";
    private const string Scopes = "openid profile email urn:zitadel:iam:org:project:roles";

    public static string ObtainUserToken(E2ESettings settings) =>
        ObtainToken(settings, settings.Auth.UserLogin, settings.Auth.UserPassword);

    public static string ObtainAdminToken(E2ESettings settings) =>
        ObtainToken(settings, settings.Auth.AdminLogin, settings.Auth.AdminPassword);

    private static string ObtainToken(E2ESettings settings, string login, string password)
    {
        var baseUrl = settings.Auth.Url.TrimEnd('/');
        var clientId = settings.Auth.ClientId;
        var pat = settings.Auth.ServiceAccountPat;
        var redirectUri = settings.GatewayUrl.TrimEnd('/') + RedirectPath;

        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };

        // 1. Generate PKCE code verifier + challenge
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var state = Guid.NewGuid().ToString("N");

        // 2. Start Authorization Code flow — don't follow redirects
        var authorizeUrl = $"{baseUrl}/oauth/v2/authorize" +
            $"?client_id={clientId}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString(Scopes)}" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256" +
            $"&state={state}";

        var authResponse = http.GetAsync(authorizeUrl).GetAwaiter().GetResult();
        if (authResponse.StatusCode is not (HttpStatusCode.Found or HttpStatusCode.SeeOther))
            throw new InvalidOperationException(
                $"Expected redirect from /oauth/v2/authorize, got {authResponse.StatusCode}");

        var location = authResponse.Headers.Location?.ToString()
            ?? throw new InvalidOperationException("Authorize response missing Location header");

        var queryStart = location.IndexOf('?');
        var authRequestId = queryStart >= 0
            ? HttpUtility.ParseQueryString(location[(queryStart + 1)..])["authRequest"]
            : null;

        if (string.IsNullOrEmpty(authRequestId))
            throw new InvalidOperationException(
                $"Could not extract authRequest from redirect: {location}");

        // 3. Create session with user + password checks (Sessions v2, requires PAT)
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);

        var sessionResponse = http.PostAsJsonAsync($"{baseUrl}/v2/sessions", new
        {
            checks = new
            {
                user = new { loginName = login },
                password = new { password }
            }
        }).GetAwaiter().GetResult();

        if (!sessionResponse.IsSuccessStatusCode)
        {
            var body = sessionResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            throw new InvalidOperationException(
                $"Failed to create session for '{login}': {sessionResponse.StatusCode} — {body}");
        }

        var sessionResult = sessionResponse.Content.ReadFromJsonAsync<JsonElement>().GetAwaiter().GetResult();
        var sessionId = sessionResult.GetProperty("sessionId").GetString()!;
        var sessionToken = sessionResult.GetProperty("sessionToken").GetString()!;

        // 4. Finalize auth request — link session to the OIDC authorization request
        var callbackResponse = http.PostAsJsonAsync(
            $"{baseUrl}/v2/oidc/auth_requests/{authRequestId}", new
            {
                session = new { sessionId, sessionToken }
            }).GetAwaiter().GetResult();

        if (!callbackResponse.IsSuccessStatusCode)
        {
            var body = callbackResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            throw new InvalidOperationException(
                $"Failed to finalize auth request '{authRequestId}': {callbackResponse.StatusCode} — {body}");
        }

        var callbackResult = callbackResponse.Content.ReadFromJsonAsync<JsonElement>().GetAwaiter().GetResult();
        var callbackUrl = callbackResult.GetProperty("callbackUrl").GetString()
            ?? throw new InvalidOperationException("Callback response missing callbackUrl");

        var code = HttpUtility.ParseQueryString(new Uri(callbackUrl).Query)["code"]
            ?? throw new InvalidOperationException(
                $"Could not extract code from callback: {callbackUrl}");

        // 5. Exchange authorization code for JWT (public client, PKCE)
        http.DefaultRequestHeaders.Authorization = null;

        var tokenResponse = http.PostAsync($"{baseUrl}/oauth/v2/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = clientId,
                ["code_verifier"] = codeVerifier
            })
        ).GetAwaiter().GetResult();

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var body = tokenResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            throw new InvalidOperationException(
                $"Token exchange failed for '{login}': {tokenResponse.StatusCode} — {body}");
        }

        var tokenResult = tokenResponse.Content.ReadFromJsonAsync<JsonElement>().GetAwaiter().GetResult();
        return tokenResult.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Token response missing access_token");
    }

    private static string GenerateCodeVerifier()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

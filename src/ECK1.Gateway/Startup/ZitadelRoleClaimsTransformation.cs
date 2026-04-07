using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ECK1.Gateway.Startup;

public class ZitadelRoleClaimsTransformation(IOptions<ZitadelConfig> zitadelOptions) : IClaimsTransformation
{
    private const string ZitadelRoleClaim = "urn:zitadel:iam:org:project:roles";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return Task.FromResult(principal);

        if (identity.HasClaim(c => c.Type == ClaimTypes.Role))
            return Task.FromResult(principal);

        var roleClaim = identity.FindFirst(ZitadelRoleClaim);
        if (roleClaim is null)
            return Task.FromResult(principal);

        var rolePermissions = zitadelOptions.Value.RolePermissions;
        var addedPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(roleClaim.Value);
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, property.Name));

                if (rolePermissions.TryGetValue(property.Name, out var permissions))
                {
                    foreach (var permission in permissions)
                    {
                        if (addedPermissions.Add(permission))
                            identity.AddClaim(new Claim("permission", permission));
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Malformed role claim — skip
        }

        return Task.FromResult(principal);
    }
}

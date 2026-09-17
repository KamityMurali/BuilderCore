using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BuilderCore.Web.Security;

public static class DevelopmentAuthenticationDefaults
{
    public const string Scheme = "Development";
}

public sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "local-dev-user"),
            new Claim("sub", "local-dev-user"),
            new Claim(ClaimTypes.Email, "developer@buildercore.local"),
            new Claim(ClaimTypes.Name, "POC Developer")
        };

        var identity = new ClaimsIdentity(claims, DevelopmentAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, DevelopmentAuthenticationDefaults.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace BuilderCore.Web.Security;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddBuilderAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var authMode = configuration["Authentication:Mode"] ?? AuthenticationModes.Development;

        if (AuthenticationModes.IsDevelopment(authMode))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Development authentication is only allowed when ASPNETCORE_ENVIRONMENT=Development.");
            }

            services.AddAuthentication(DevelopmentAuthenticationDefaults.Scheme)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
                    DevelopmentAuthenticationDefaults.Scheme,
                    _ => { });
        }
        else if (AuthenticationModes.IsEntra(authMode))
        {
            var entra = configuration.GetSection(EntraOptions.SectionName).Get<EntraOptions>()
                ?? throw new InvalidOperationException($"Configuration section '{EntraOptions.SectionName}' is required.");
            entra.Validate();

            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = entra.GetAuthority();
                    options.ClientId = entra.ClientId;
                    options.CallbackPath = entra.CallbackPath;
                    options.SignedOutCallbackPath = entra.SignedOutCallbackPath;
                    options.SignedOutRedirectUri = entra.SignedOutRedirectUri;

                    if (!string.IsNullOrWhiteSpace(entra.ClientSecret))
                    {
                        options.ClientSecret = entra.ClientSecret;
                    }

                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.SaveTokens = false;
                    options.GetClaimsFromUserInfoEndpoint = true;
                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters.NameClaimType = "name";
                    options.TokenValidationParameters.RoleClaimType = "roles";
                });
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown Authentication:Mode '{authMode}'. Supported values: {AuthenticationModes.Development}, {AuthenticationModes.Entra}.");
        }

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.AuthenticatedUser, policy => policy.RequireAuthenticatedUser());
        });

        return services;
    }
}

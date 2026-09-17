using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace BuilderCore.Web.Security;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddBuilderAuthentication(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var authMode = configuration["Authentication:Mode"] ?? "Development";

        if (string.Equals(authMode, "Development", StringComparison.OrdinalIgnoreCase))
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
        else if (string.Equals(authMode, "Cognito", StringComparison.OrdinalIgnoreCase))
        {
            var cognito = configuration.GetSection("Authentication:Cognito");
            var authority = cognito["Authority"] ?? throw new InvalidOperationException("Authentication:Cognito:Authority is required.");
            var clientId = cognito["ClientId"] ?? throw new InvalidOperationException("Authentication:Cognito:ClientId is required.");
            var clientSecret = cognito["ClientSecret"];
            var metadataAddress = cognito["MetadataAddress"];
            var signedOutRedirectUri = cognito["SignedOutRedirectUri"] ?? "/";

            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = authority;
                    options.ClientId = clientId;
                    if (!string.IsNullOrWhiteSpace(clientSecret))
                    {
                        options.ClientSecret = clientSecret;
                    }

                    if (!string.IsNullOrWhiteSpace(metadataAddress))
                    {
                        options.MetadataAddress = metadataAddress;
                    }

                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.SaveTokens = false;
                    options.GetClaimsFromUserInfoEndpoint = true;
                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("email");
                    options.Scope.Add("profile");
                    options.SignedOutRedirectUri = signedOutRedirectUri;
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters.NameClaimType = "name";
                    options.TokenValidationParameters.RoleClaimType = "cognito:groups";
                });
        }
        else
        {
            throw new InvalidOperationException($"Unknown Authentication:Mode '{authMode}'.");
        }

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
            options.AddPolicy(Policies.AuthenticatedUser, policy => policy.RequireAuthenticatedUser());
        });

        return services;
    }
}

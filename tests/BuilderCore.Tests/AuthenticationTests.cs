using System.Security.Claims;
using BuilderCore.Web.Security;
using BuilderCore.Web.Services;
using BuilderCore.Tests.TestFixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BuilderCore.Tests;

public class AuthenticationTests
{
    [Fact]
    public void CurrentUserService_ReadsEntraSubClaim()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "entra-sub-123"),
            new Claim("email", "user@example.com"),
            new Claim("name", "Example User")
        ], "test"));

        var accessor = new HttpContextAccessor { HttpContext = context };
        var service = new CurrentUserService(accessor);

        Assert.True(service.IsAuthenticated);
        Assert.Equal("entra-sub-123", service.UserId);
        Assert.Equal("user@example.com", service.Email);
        Assert.Equal("Example User", service.DisplayName);
    }

    [Fact]
    public void CurrentUserService_ReadsPreferredUsernameWhenEmailMissing()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "entra-sub-456"),
            new Claim("preferred_username", "user@contoso.com"),
            new Claim("name", "Contoso User")
        ], "test"));

        var accessor = new HttpContextAccessor { HttpContext = context };
        var service = new CurrentUserService(accessor);

        Assert.Equal("user@contoso.com", service.Email);
    }

    [Fact]
    public void CurrentUserService_Unauthenticated_ReturnsEmptyUserId()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var service = new CurrentUserService(accessor);

        Assert.False(service.IsAuthenticated);
        Assert.Equal(string.Empty, service.UserId);
        Assert.Equal("User", service.DisplayName);
    }

    [Fact]
    public void TestCurrentUserService_UsesSyntheticClaims()
    {
        var service = new TestCurrentUserService("local-dev-user", "developer@buildercore.local", "POC Developer");
        Assert.Equal("local-dev-user", service.UserId);
        Assert.Equal("developer@buildercore.local", service.Email);
    }

    [Fact]
    public void AuthenticationModes_UsesOpenIdConnect_OnlyForEntra()
    {
        Assert.True(AuthenticationModes.UsesOpenIdConnect(AuthenticationModes.Entra));
        Assert.True(AuthenticationModes.UsesOpenIdConnect("entra"));
        Assert.False(AuthenticationModes.UsesOpenIdConnect(AuthenticationModes.Development));
        Assert.False(AuthenticationModes.UsesOpenIdConnect("Cognito"));
    }

    [Fact]
    public void AddBuilderAuthentication_Development_DoesNotSetFallbackPolicy()
    {
        var options = GetAuthorizationOptions(new Dictionary<string, string?>
        {
            ["Authentication:Mode"] = AuthenticationModes.Development
        });

        Assert.Null(options.FallbackPolicy);
        AssertAuthenticatedUserPolicy(options);
    }

    [Fact]
    public void AddBuilderAuthentication_Entra_DoesNotSetFallbackPolicy()
    {
        var options = GetAuthorizationOptions(new Dictionary<string, string?>
        {
            ["Authentication:Mode"] = AuthenticationModes.Entra,
            ["Authentication:Entra:TenantId"] = "tenant-id",
            ["Authentication:Entra:ClientId"] = "client-id",
            ["Authentication:Entra:ClientSecret"] = "secret"
        });

        Assert.Null(options.FallbackPolicy);
        AssertAuthenticatedUserPolicy(options);
    }

    private static AuthorizationOptions GetAuthorizationOptions(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBuilderAuthentication(configuration, environment);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
    }

    private static void AssertAuthenticatedUserPolicy(AuthorizationOptions options)
    {
        var policy = options.GetPolicy(Policies.AuthenticatedUser);
        Assert.NotNull(policy);
        Assert.Contains(policy.Requirements, requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }
}

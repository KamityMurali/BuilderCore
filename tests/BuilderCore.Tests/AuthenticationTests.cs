using System.Security.Claims;
using BuilderCore.Web.Services;
using BuilderCore.Tests.TestFixtures;
using Microsoft.AspNetCore.Http;

namespace BuilderCore.Tests;

public class AuthenticationTests
{
    [Fact]
    public void CurrentUserService_ReadsCognitoSub()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "cognito-sub-123"),
            new Claim("email", "user@example.com"),
            new Claim("name", "Example User")
        ], "test"));

        var accessor = new HttpContextAccessor { HttpContext = context };
        var service = new CurrentUserService(accessor);

        Assert.True(service.IsAuthenticated);
        Assert.Equal("cognito-sub-123", service.UserId);
        Assert.Equal("user@example.com", service.Email);
        Assert.Equal("Example User", service.DisplayName);
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
}

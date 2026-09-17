using BuilderCore.Web.Security;

namespace BuilderCore.Tests;

public class EntraOptionsTests
{
    [Fact]
    public void GetAuthority_BuildsMicrosoftIdentityPlatformUrl()
    {
        var options = new EntraOptions
        {
            TenantId = "11111111-1111-1111-1111-111111111111",
            ClientId = "client-id",
            Instance = "https://login.microsoftonline.com/"
        };

        Assert.Equal(
            "https://login.microsoftonline.com/11111111-1111-1111-1111-111111111111/v2.0",
            options.GetAuthority());
    }

    [Fact]
    public void Validate_ThrowsWhenTenantIdMissing()
    {
        var options = new EntraOptions { ClientId = "client-id" };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("TenantId", ex.Message);
    }

    [Fact]
    public void Validate_ThrowsWhenClientIdMissing()
    {
        var options = new EntraOptions { TenantId = "tenant-id" };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("ClientId", ex.Message);
    }
}

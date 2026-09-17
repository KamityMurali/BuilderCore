using System.Net;
using BuilderCore.Web.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace BuilderCore.Tests;

public sealed class EntraWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        var dbPath = Path.Combine(Path.GetTempPath(), $"buildercore-entra-test-{Guid.NewGuid():N}.db");
        var previous = new Dictionary<string, string?>();
        var values = new Dictionary<string, string?>
        {
            ["Authentication__Mode"] = AuthenticationModes.Entra,
            ["Authentication__Entra__TenantId"] = "common",
            ["Authentication__Entra__ClientId"] = "11111111-1111-1111-1111-111111111111",
            ["Authentication__Entra__ClientSecret"] = "test-secret",
            ["ConnectionStrings__DefaultConnection"] = $"Data Source={dbPath};Foreign Keys=True",
            ["DemoData__Enabled"] = "false"
        };

        foreach (var (key, value) in values)
        {
            previous[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }

        try
        {
            return base.CreateHost(builder);
        }
        finally
        {
            foreach (var (key, value) in previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}

public class EntraStaticResourceTests : IClassFixture<EntraWebApplicationFactory>
{
    private readonly HttpClient _client;

    public EntraStaticResourceTests(EntraWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Theory]
    [InlineData("/css/site.css")]
    [InlineData("/js/theme.js")]
    [InlineData("/_framework/blazor.web.js")]
    public async Task StaticResources_ReturnTheAsset_WithoutEntraChallenge(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Login_AllowsAnonymousAndChallengesOpenIdConnect()
    {
        var response = await _client.GetAsync("/login");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Redirect,
            $"{response.StatusCode}: {body}");
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        Assert.Contains("login.microsoftonline.com", location, StringComparison.OrdinalIgnoreCase);
    }
}

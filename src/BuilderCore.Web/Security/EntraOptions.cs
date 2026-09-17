namespace BuilderCore.Web.Security;

public sealed class EntraOptions
{
    public const string SectionName = "Authentication:Entra";

    public string TenantId { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string? ClientSecret { get; set; }

    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    public string CallbackPath { get; set; } = "/signin-oidc";

    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    public string SignedOutRedirectUri { get; set; } = "/";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TenantId))
        {
            throw new InvalidOperationException($"{SectionName}:TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new InvalidOperationException($"{SectionName}:ClientId is required.");
        }
    }

    public string GetAuthority() =>
        $"{Instance.TrimEnd('/')}/{TenantId.Trim('/')}/v2.0";
}

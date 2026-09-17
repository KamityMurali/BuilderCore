namespace BuilderCore.Web.Security;

public static class AuthenticationModes
{
    public const string Development = "Development";
    public const string Entra = "Entra";

    public static bool IsEntra(string? mode) =>
        string.Equals(mode, Entra, StringComparison.OrdinalIgnoreCase);

    public static bool IsDevelopment(string? mode) =>
        string.Equals(mode, Development, StringComparison.OrdinalIgnoreCase);

    public static bool UsesOpenIdConnect(string? mode) => IsEntra(mode);
}

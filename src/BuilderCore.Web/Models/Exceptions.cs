namespace BuilderCore.Web.Models;

public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}

public sealed class DbUpdateConcurrencyException : Exception
{
    public const string UserMessage = "This record was changed by another user. Reload and try again.";

    public DbUpdateConcurrencyException() : base(UserMessage) { }
}

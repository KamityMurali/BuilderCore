using System.Globalization;

namespace BuilderCore.Web.Components;

public static class CurrencyHelper
{
    private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");

    public static string Format(decimal amount) => amount.ToString("C2", UsCulture);
}

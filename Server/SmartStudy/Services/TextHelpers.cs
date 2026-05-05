using System.Text.RegularExpressions;

namespace SmartStudy.Services;

// Tiny string utilities shared across services and controllers.
public static class TextHelpers
{
    private static readonly Regex GcalTagRegex = new(@"\s*\[gcal:[^\]]*\]", RegexOptions.Compiled);

    // Removes any "[gcal:...]" sync marker from a description so the user sees clean text.
    public static string? StripGcalTag(string? text) =>
        text == null ? null : GcalTagRegex.Replace(text, "").Trim();
}

using System.Text.RegularExpressions;

namespace SmartStudy.Services;

public static class TextHelpers
{
    private static readonly Regex GcalTagRegex = new(@"\s*\[gcal:[^\]]*\]", RegexOptions.Compiled);

    public static string? StripGcalTag(string? text) =>
        text == null ? null : GcalTagRegex.Replace(text, "").Trim();
}

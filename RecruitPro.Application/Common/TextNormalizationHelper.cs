using System.Globalization;
using System.Linq;

namespace RecruitPro.Application.Common;

public static class TextNormalizationHelper
{
    public static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = RemoveInvalidDatabaseCharacters(value).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    /// <summary>
    /// Reflows a person name that arrived in ALL CAPS (common in CVs) to Title Case, e.g.
    /// "PHÙNG NHẬT QUANG" -> "Phùng Nhật Quang". Names with any lowercase letter are left as-is so
    /// intentional casing (e.g. "McDonald", "de la Cruz") is respected.
    /// </summary>
    public static string? NormalizePersonName(string? value)
    {
        string? normalized = NormalizeOptionalText(value);
        if (normalized == null || normalized.Any(char.IsLower))
        {
            return normalized;
        }

        // ToTitleCase ignores strings that are entirely uppercase, so lowercase first.
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
    }

    public static string RemoveInvalidDatabaseCharacters(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\0", string.Empty);
    }
}

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

    public static string RemoveInvalidDatabaseCharacters(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\0", string.Empty);
    }
}

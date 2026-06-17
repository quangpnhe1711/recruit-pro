namespace RecruitPro.Application.Common;

public static class TextNormalizationHelper
{
    public static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

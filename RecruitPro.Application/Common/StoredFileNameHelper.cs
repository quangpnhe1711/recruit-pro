namespace RecruitPro.Application.Common;

public static class StoredFileNameHelper
{
    public static string ExtractDisplayFileName(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return string.Empty;
        }

        string fileName = Path.GetFileName(
            Uri.TryCreate(storedValue, UriKind.Absolute, out Uri? uri)
                ? uri.AbsolutePath
                : storedValue);

        int separatorIndex = fileName.IndexOf('_');
        return separatorIndex >= 0 && separatorIndex < fileName.Length - 1
            ? fileName[(separatorIndex + 1)..]
            : fileName;
    }
}

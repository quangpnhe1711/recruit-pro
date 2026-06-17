using AutoMapper;
using System.Text.Json;

namespace RecruitPro.Application.Mappings;

public sealed class JsonStringListValueConverter : IValueConverter<string?, List<string>>
{
    public List<string> Convert(string? sourceMember, ResolutionContext context)
    {
        if (string.IsNullOrWhiteSpace(sourceMember))
        {
            return [];
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(sourceMember);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                return document.RootElement
                    .EnumerateArray()
                    .Select(element => element.GetString() ?? string.Empty)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToList();
            }
        }
        catch
        {
        }

        return [sourceMember];
    }
}

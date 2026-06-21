using System.Text;
using System.Text.Json;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Common;

public static class CandidateProfileSectionHelper
{
    public static IReadOnlyList<CandidateSectionSnapshot> BuildSections(CandidateProfile profile)
    {
        List<CandidateSectionSnapshot> storedSections = profile.Sections
            .OrderBy(section => section.DisplayOrder)
            .Select(section => new CandidateSectionSnapshot
            {
                Id = section.Id,
                SectionKey = section.SectionKey,
                Title = section.Title,
                SectionType = section.SectionType,
                Source = section.Source,
                DisplayOrder = section.DisplayOrder,
                SchemaJson = section.SchemaJson,
                Items = section.Items
                    .OrderBy(item => item.DisplayOrder)
                    .Select(item => new CandidateSectionItemSnapshot
                    {
                        Id = item.Id,
                        ItemType = item.ItemType,
                        Title = item.Title,
                        Subtitle = item.Subtitle,
                        Organization = item.Organization,
                        Location = item.Location,
                        Description = item.Description,
                        DateLabel = item.DateLabel,
                        StartMonth = item.StartMonth,
                        StartYear = item.StartYear,
                        EndMonth = item.EndMonth,
                        EndYear = item.EndYear,
                        IsCurrent = item.IsCurrent,
                        DisplayOrder = item.DisplayOrder,
                        Tags = DeserializeStringList(item.TagsJson),
                        Attributes = DeserializeDictionary(item.AttributesJson)
                    })
                    .ToList()
            })
            .ToList();

        if (storedSections.Count > 0)
        {
            return storedSections;
        }

        return BuildLegacySections(profile);
    }

    public static string BuildStructuredNarrative(CandidateProfile profile)
    {
        StringBuilder builder = new();
        Append(builder, profile.CurrentPosition);
        Append(builder, profile.Bio);

        foreach (CandidateSectionSnapshot section in BuildSections(profile))
        {
            if (!string.IsNullOrWhiteSpace(section.Title))
            {
                builder.AppendLine(section.Title.Trim() + ":");
            }

            foreach (CandidateSectionItemSnapshot item in section.Items.OrderBy(entry => entry.DisplayOrder))
            {
                List<string> parts = [];
                AddPart(parts, item.Title);
                AddPart(parts, item.Subtitle);
                AddPart(parts, item.Organization);
                AddPart(parts, item.Location);
                AddPart(parts, item.DateLabel);

                if (item.StartYear.HasValue)
                {
                    string start = item.StartMonth.HasValue ? $"{item.StartMonth:00}/{item.StartYear}" : item.StartYear.Value.ToString();
                    string end = item.IsCurrent
                        ? "Present"
                        : item.EndYear.HasValue
                            ? item.EndMonth.HasValue ? $"{item.EndMonth:00}/{item.EndYear}" : item.EndYear.Value.ToString()
                            : null;

                    if (!string.IsNullOrWhiteSpace(end))
                    {
                        AddPart(parts, $"{start} - {end}");
                    }
                    else
                    {
                        AddPart(parts, start);
                    }
                }

                if (item.Tags.Count > 0)
                {
                    AddPart(parts, string.Join(", ", item.Tags));
                }

                if (item.Attributes.Count > 0)
                {
                    AddPart(parts, string.Join("; ", item.Attributes.Select(attribute => $"{attribute.Key}: {attribute.Value}")));
                }

                if (parts.Count > 0)
                {
                    builder.AppendLine(string.Join(" | ", parts));
                }

                Append(builder, item.Description);
            }

            if (section.Items.Count > 0)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString().Trim();
    }

    private static IReadOnlyList<CandidateSectionSnapshot> BuildLegacySections(CandidateProfile profile)
    {
        List<CandidateSectionSnapshot> sections = [];

        List<CandidateSectionItemSnapshot> experienceItems = LoadDocuments<LegacyExperienceDocument>(profile.ExperienceEntriesJson)
            .Select((entry, index) => new CandidateSectionItemSnapshot
            {
                Id = Guid.TryParse(entry.Id, out Guid id) ? id : Guid.NewGuid(),
                ItemType = "Experience",
                Title = entry.Title,
                Organization = entry.Company,
                Description = entry.Bullets.Count == 0 ? null : string.Join(Environment.NewLine, entry.Bullets),
                StartMonth = entry.Period.StartMonth,
                StartYear = entry.Period.StartYear,
                EndMonth = entry.Period.EndMonth,
                EndYear = entry.Period.EndYear,
                IsCurrent = entry.Period.IsCurrent,
                DisplayOrder = index
            })
            .ToList();

        if (experienceItems.Count > 0)
        {
            sections.Add(new CandidateSectionSnapshot
            {
                Id = Guid.Empty,
                SectionKey = "experience",
                Title = "Experience",
                SectionType = "Timeline",
                Source = "Legacy",
                DisplayOrder = 100,
                Items = experienceItems
            });
        }

        List<CandidateSectionItemSnapshot> projectItems = profile.Projects
            .OrderBy(item => item.StartYear)
            .ThenBy(item => item.StartMonth)
            .Select((item, index) => new CandidateSectionItemSnapshot
            {
                Id = item.Id,
                ItemType = "Project",
                Title = item.Name,
                Subtitle = item.Role,
                Description = item.Description,
                StartMonth = item.StartMonth,
                StartYear = item.StartYear,
                EndMonth = item.EndMonth,
                EndYear = item.EndYear,
                IsCurrent = item.IsCurrent,
                DisplayOrder = index,
                Tags = DeserializeStringList(item.TechnologiesJson)
            })
            .ToList();

        if (projectItems.Count > 0)
        {
            sections.Add(new CandidateSectionSnapshot
            {
                Id = Guid.Empty,
                SectionKey = "projects",
                Title = "Projects",
                SectionType = "Portfolio",
                Source = "Legacy",
                DisplayOrder = 200,
                Items = projectItems
            });
        }

        List<CandidateSectionItemSnapshot> educationItems = LoadDocuments<LegacyEducationDocument>(profile.EducationRecordsJson)
            .Select((item, index) => new CandidateSectionItemSnapshot
            {
                Id = Guid.TryParse(item.Id, out Guid id) ? id : Guid.NewGuid(),
                ItemType = "Education",
                Title = item.School,
                Subtitle = item.Degree,
                Description = item.Description,
                StartYear = item.StartYear,
                EndYear = item.EndYear,
                DisplayOrder = index,
                Attributes = BuildAttributes(("fieldOfStudy", item.FieldOfStudy))
            })
            .ToList();

        if (educationItems.Count > 0)
        {
            sections.Add(new CandidateSectionSnapshot
            {
                Id = Guid.Empty,
                SectionKey = "education",
                Title = "Education",
                SectionType = "Education",
                Source = "Legacy",
                DisplayOrder = 300,
                Items = educationItems
            });
        }

        List<CandidateSectionItemSnapshot> certificationItems = LoadDocuments<LegacyCertificationDocument>(profile.CertificationRecordsJson)
            .Select((item, index) => new CandidateSectionItemSnapshot
            {
                Id = Guid.TryParse(item.Id, out Guid id) ? id : Guid.NewGuid(),
                ItemType = "Certification",
                Title = item.Name,
                Organization = item.Issuer,
                DateLabel = item.IssuedOn?.ToString("yyyy-MM-dd"),
                DisplayOrder = index,
                Attributes = BuildAttributes(
                    ("expiresOn", item.ExpiresOn?.ToString("yyyy-MM-dd")),
                    ("credentialId", item.CredentialId),
                    ("credentialUrl", item.CredentialUrl))
            })
            .ToList();

        if (certificationItems.Count > 0)
        {
            sections.Add(new CandidateSectionSnapshot
            {
                Id = Guid.Empty,
                SectionKey = "certifications",
                Title = "Certifications",
                SectionType = "Achievements",
                Source = "Legacy",
                DisplayOrder = 400,
                Items = certificationItems
            });
        }

        List<CandidateSectionItemSnapshot> languageItems = LoadDocuments<LegacyLanguageDocument>(profile.LanguageRecordsJson)
            .Select((item, index) => new CandidateSectionItemSnapshot
            {
                Id = Guid.TryParse(item.Id, out Guid id) ? id : Guid.NewGuid(),
                ItemType = "Language",
                Title = item.Name,
                Subtitle = item.Proficiency,
                DisplayOrder = index
            })
            .ToList();

        if (languageItems.Count > 0)
        {
            sections.Add(new CandidateSectionSnapshot
            {
                Id = Guid.Empty,
                SectionKey = "languages",
                Title = "Languages",
                SectionType = "Attributes",
                Source = "Legacy",
                DisplayOrder = 500,
                Items = languageItems
            });
        }

        return sections;
    }

    private static Dictionary<string, string> BuildAttributes(params (string Key, string? Value)[] values)
    {
        return values
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(item => item.Key, item => item.Value!, StringComparer.OrdinalIgnoreCase);
    }

    private static void AddPart(List<string> parts, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add(value.Trim());
        }
    }

    private static void Append(StringBuilder builder, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine(value.Trim());
        }
    }

    private static List<TDocument> LoadDocuments<TDocument>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<TDocument>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static Dictionary<string, string> DeserializeDictionary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private sealed class LegacyExperienceDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public LegacyPeriodDocument Period { get; set; } = new();
        public List<string> Bullets { get; set; } = [];
    }

    private sealed class LegacyPeriodDocument
    {
        public int StartMonth { get; set; }
        public int StartYear { get; set; }
        public int? EndMonth { get; set; }
        public int? EndYear { get; set; }
        public bool IsCurrent { get; set; }
    }

    private sealed class LegacyEducationDocument
    {
        public string Id { get; set; } = string.Empty;
        public string School { get; set; } = string.Empty;
        public string Degree { get; set; } = string.Empty;
        public string? FieldOfStudy { get; set; }
        public int? StartYear { get; set; }
        public int? EndYear { get; set; }
        public string? Description { get; set; }
    }

    private sealed class LegacyCertificationDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Issuer { get; set; }
        public DateTime? IssuedOn { get; set; }
        public DateTime? ExpiresOn { get; set; }
        public string? CredentialId { get; set; }
        public string? CredentialUrl { get; set; }
    }

    private sealed class LegacyLanguageDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Proficiency { get; set; } = string.Empty;
    }
}

public class CandidateSectionSnapshot
{
    public Guid Id { get; set; }
    public string? SectionKey { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SectionType { get; set; } = "Custom";
    public string Source { get; set; } = "User";
    public int DisplayOrder { get; set; }
    public string? SchemaJson { get; set; }
    public List<CandidateSectionItemSnapshot> Items { get; set; } = [];
}

public class CandidateSectionItemSnapshot
{
    public Guid Id { get; set; }
    public string ItemType { get; set; } = "Entry";
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Organization { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string? DateLabel { get; set; }
    public int? StartMonth { get; set; }
    public int? StartYear { get; set; }
    public int? EndMonth { get; set; }
    public int? EndYear { get; set; }
    public bool IsCurrent { get; set; }
    public int DisplayOrder { get; set; }
    public List<string> Tags { get; set; } = [];
    public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

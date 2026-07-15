using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Services;

/// <summary>
/// Pure resume text-extraction and heuristic preview-building. Split out of CandidateService; its only
/// dependency is the PDF text extractor. Contains no persistence, storage, AI-provider or mapping logic.
/// </summary>
public class ResumeParsingService : IResumeParsingService
{
    private readonly IResumeTextExtractor _resumeTextExtractor;

    private const int MinimumResumeTextLength = 50;
    private static readonly Regex EmailExtractorPattern = new(@"(?<email>[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PhoneExtractorPattern = new(@"(?<phone>(?:\+?84|0)[\s\-.]?(?:\d[\s\-.]?){8,10})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex UrlPattern = new(@"https?://[^\s)]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex YearPattern = new(@"\b(19|20)\d{2}\b", RegexOptions.Compiled);

    public ResumeParsingService(IResumeTextExtractor resumeTextExtractor)
    {
        _resumeTextExtractor = resumeTextExtractor;
    }

    /// <summary>
    /// Extracts resume text.
    /// </summary>
    /// <param name="resumeStream">The <paramref name="resumeStream"/> value.</param>
    /// <param name="fileName">The <paramref name="fileName"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    /// <exception cref="NotSupportedException">Thrown when the operation fails validation or encounters an invalid state.</exception>
    public async Task<string> ExtractResumeTextAsync(Stream resumeStream, string fileName)
    {
        string extension = Path.GetExtension(fileName).Trim().ToLowerInvariant();
        if (resumeStream.CanSeek)
        {
            resumeStream.Position = 0;
        }

        return extension switch
        {
            ".pdf" => await _resumeTextExtractor.ExtractTextAsync(resumeStream),
            ".docx" => await ExtractDocxTextAsync(resumeStream),
            ".txt" => await ReadPlainTextAsync(resumeStream),
            ".doc" => throw new NotSupportedException("Legacy .doc resumes are not supported for parsing yet. Please upload a PDF or DOCX file."),
            _ => throw new NotSupportedException("Unsupported resume format. Please upload a PDF or DOCX file.")
        };
    }

    /// <summary>
    /// Executes the read plain text operation.
    /// </summary>
    /// <param name="stream">The <paramref name="stream"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    private async Task<string> ReadPlainTextAsync(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using StreamReader reader = new(stream, leaveOpen: true);
        string content = await reader.ReadToEndAsync();
        return NormalizeResumeText(content);
    }

    /// <summary>
    /// Extracts docx text.
    /// </summary>
    /// <param name="stream">The <paramref name="stream"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    private async Task<string> ExtractDocxTextAsync(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: true);
        ZipArchiveEntry? documentEntry = archive.GetEntry("word/document.xml");
        if (documentEntry == null)
        {
            return string.Empty;
        }

        await using Stream entryStream = documentEntry.Open();
        using StreamReader reader = new(entryStream);
        string xml = await reader.ReadToEndAsync();
        string text = Regex.Replace(xml, "<w:tab[^>]*/>", "\t", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</w:p>", Environment.NewLine, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        return NormalizeResumeText(text);
    }

    /// <summary>
    /// Builds resume parse preview.
    /// </summary>
    /// <param name="extractedText">The <paramref name="extractedText"/> value.</param>
    /// <param name="allSkills">The <paramref name="allSkills"/> value.</param>
    /// <returns>The operation result.</returns>
    public CandidateResumeParseResponseDto BuildResumeParsePreview(string extractedText, IReadOnlyList<Skill> allSkills)
    {
        string normalizedText = NormalizeResumeText(extractedText);
        List<string> lines = normalizedText
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        Dictionary<string, List<string>> sections = ExtractResumeSections(lines);

        string email = ExtractEmail(normalizedText) ?? string.Empty;
        string phone = ExtractPhone(normalizedText) ?? string.Empty;
        string github = ExtractUrl(normalizedText, "github.com") ?? string.Empty;
        string linkedin = ExtractUrl(normalizedText, "linkedin.com") ?? string.Empty;
        string name = ExtractCandidateName(lines) ?? string.Empty;
        string headline = ExtractHeadline(lines, name) ?? string.Empty;
        string location = ExtractLocation(lines) ?? string.Empty;
        string bio = ExtractSummary(sections) ?? string.Empty;

        List<CandidateSkillViewDto> parsedSkills = MatchSkills(normalizedText, allSkills)
            .Select(skill => new CandidateSkillViewDto
            {
                Id = skill.Id.ToString(),
                Label = skill.Name,
                Active = true,
                YearsOfExperience = ExtractYearsOfExperienceForSkill(normalizedText, skill.Name)
            })
            .ToList();

        List<CandidateExperienceDto> experiences = ParseExperienceEntries(sections.TryGetValue("experience", out List<string>? experienceLines) ? experienceLines : lines, "Experience", defaultCompany: "Not specified");
        List<CandidateProjectDto> projects = ParseProjects(sections.TryGetValue("projects", out List<string>? projectLines) ? projectLines : []);
        List<CandidateEducationDto> educations = ParseEducations(sections.TryGetValue("education", out List<string>? educationLines) ? educationLines : []);
        List<CandidateCertificationDto> certifications = ParseCertifications(sections.TryGetValue("certifications", out List<string>? certificationLines) ? certificationLines : []);
        List<CandidateLanguageDto> languages = ParseLanguages(sections.TryGetValue("languages", out List<string>? languageLines) ? languageLines : []);

        List<string> notes = [];
        notes.Add($"Detected {parsedSkills.Count} skills from the resume.");
        if (experiences.Count > 0)
        {
            notes.Add($"Detected {experiences.Count} work experience entries.");
        }

        if (projects.Count > 0)
        {
            notes.Add($"Detected {projects.Count} projects.");
        }

        if (educations.Count > 0)
        {
            notes.Add($"Detected {educations.Count} education records.");
        }

        if (certifications.Count > 0)
        {
            notes.Add($"Detected {certifications.Count} certifications.");
        }

        if (languages.Count > 0)
        {
            notes.Add($"Detected {languages.Count} languages.");
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
        {
            notes.Add("Some personal information could not be confidently extracted. Please review carefully before saving.");
        }

        return new CandidateResumeParseResponseDto
        {
            UsedAi = false,
            ParsingMode = "Heuristic",
            Profile = new CandidateResumeParseProfileDto
            {
                Name = TextNormalizationHelper.NormalizePersonName(name) ?? string.Empty,
                Headline = headline,
                Email = email,
                Phone = phone,
                Location = location,
                Bio = bio,
                Github = github,
                Linkedin = linkedin
            },
            Skills = parsedSkills,
            ExperienceEntries = experiences,
            Projects = projects,
            Educations = educations,
            Certifications = certifications,
            Languages = languages,
            Sections = BuildSectionsFromParsedPreview(experiences, projects, educations, certifications, languages, [], []),
            Notes = notes,
            ExtractedTextPreview = string.Join(Environment.NewLine, lines.Take(40))
        };
    }

    /// <summary>
    /// Builds resume parse preview from ai.
    /// </summary>
    /// <param name="aiPreview">The <paramref name="aiPreview"/> value.</param>
    /// <param name="extractedText">The <paramref name="extractedText"/> value.</param>
    /// <param name="allSkills">The <paramref name="allSkills"/> value.</param>
    /// <param name="modelName">The <paramref name="modelName"/> value.</param>
    /// <returns>The operation result.</returns>
    public CandidateResumeParseResponseDto BuildResumeParsePreviewFromAi(
        CandidateResumeAiParseDto aiPreview,
        string extractedText,
        IReadOnlyList<Skill> allSkills,
        string? modelName)
    {
        Dictionary<string, Skill> skillLookup = allSkills
            .GroupBy(skill => skill.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        List<CandidateSkillViewDto> parsedSkills = aiPreview.Skills
            .Where(skill => !string.IsNullOrWhiteSpace(skill.Name))
            .Select(skill =>
            {
                Skill? matchedSkill = skillLookup.TryGetValue(skill.Name.Trim(), out Skill? foundSkill)
                    ? foundSkill
                    : skillLookup.Values.FirstOrDefault(value =>
                        value.Name.Contains(skill.Name.Trim(), StringComparison.OrdinalIgnoreCase)
                        || skill.Name.Trim().Contains(value.Name, StringComparison.OrdinalIgnoreCase));

                return new CandidateSkillViewDto
                {
                    Id = matchedSkill?.Id.ToString() ?? Guid.NewGuid().ToString(),
                    Label = matchedSkill?.Name ?? skill.Name.Trim(),
                    Active = true,
                    YearsOfExperience = skill.YearsOfExperience
                };
            })
            .DistinctBy(skill => skill.Label.ToLowerInvariant())
            .ToList();

        return new CandidateResumeParseResponseDto
        {
            UsedAi = true,
            ParsingMode = "AI",
            ModelName = modelName,
            Profile = new CandidateResumeParseProfileDto
            {
                Name = TextNormalizationHelper.NormalizePersonName(aiPreview.Profile.Name) ?? string.Empty,
                Headline = aiPreview.Profile.Headline ?? string.Empty,
                Email = aiPreview.Profile.Email ?? string.Empty,
                Phone = aiPreview.Profile.Phone ?? string.Empty,
                Location = aiPreview.Profile.Location ?? string.Empty,
                Bio = aiPreview.Profile.Summary ?? string.Empty,
                Github = aiPreview.Profile.Github ?? string.Empty,
                Linkedin = aiPreview.Profile.Linkedin ?? string.Empty
            },
            Skills = parsedSkills,
            ExperienceEntries = aiPreview.ExperienceEntries
                .Select(item => new CandidateExperienceDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = string.IsNullOrWhiteSpace(item.Title) ? "Experience" : item.Title.Trim(),
                    Company = string.IsNullOrWhiteSpace(item.Company) ? "Not specified" : item.Company.Trim(),
                    Period = new CandidateExperiencePeriodDto
                    {
                        StartMonth = item.StartMonth ?? 1,
                        StartYear = item.StartYear ?? DateTime.UtcNow.Year,
                        EndMonth = item.IsCurrent ? null : item.EndMonth,
                        EndYear = item.IsCurrent ? null : item.EndYear,
                        IsCurrent = item.IsCurrent
                    },
                    Bullets = item.Bullets
                        .Select(value => value.Trim())
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToList()
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Title))
                .ToList(),
            Projects = aiPreview.Projects
                .Select(item => new CandidateProjectDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = string.IsNullOrWhiteSpace(item.Name) ? "Project" : item.Name.Trim(),
                    Role = TextNormalizationHelper.NormalizeOptionalText(item.Role),
                    Description = TextNormalizationHelper.NormalizeOptionalText(item.Description),
                    Technologies = item.Technologies
                        .Select(value => value.Trim())
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    Period = new CandidateExperiencePeriodDto
                    {
                        StartMonth = item.StartMonth ?? 1,
                        StartYear = item.StartYear ?? DateTime.UtcNow.Year,
                        EndMonth = item.IsCurrent ? null : item.EndMonth,
                        EndYear = item.IsCurrent ? null : item.EndYear,
                        IsCurrent = item.IsCurrent
                    }
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .ToList(),
            Educations = aiPreview.Educations
                .Select(item => new CandidateEducationDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    School = item.School.Trim(),
                    Degree = item.Degree.Trim(),
                    FieldOfStudy = TextNormalizationHelper.NormalizeOptionalText(item.FieldOfStudy),
                    StartYear = item.StartYear,
                    EndYear = item.EndYear,
                    Description = TextNormalizationHelper.NormalizeOptionalText(item.Description)
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.School) && !string.IsNullOrWhiteSpace(item.Degree))
                .ToList(),
            Certifications = aiPreview.Certifications
                .Select(item => new CandidateCertificationDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = item.Name.Trim(),
                    Issuer = TextNormalizationHelper.NormalizeOptionalText(item.Issuer),
                    IssuedOn = item.IssuedOn,
                    ExpiresOn = item.ExpiresOn,
                    CredentialId = TextNormalizationHelper.NormalizeOptionalText(item.CredentialId),
                    CredentialUrl = TextNormalizationHelper.NormalizeOptionalText(item.CredentialUrl)
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .ToList(),
            Languages = aiPreview.Languages
                .Select(item => new CandidateLanguageDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = item.Name.Trim(),
                    Proficiency = string.IsNullOrWhiteSpace(item.Proficiency) ? "Unspecified" : item.Proficiency.Trim()
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .DistinctBy(item => item.Name.ToLowerInvariant())
                .ToList(),
            Sections = BuildSectionsFromParsedPreview(
                aiPreview.ExperienceEntries
                    .Select(item => new CandidateExperienceDto
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Title = string.IsNullOrWhiteSpace(item.Title) ? "Experience" : item.Title.Trim(),
                        Company = string.IsNullOrWhiteSpace(item.Company) ? "Not specified" : item.Company.Trim(),
                        Period = new CandidateExperiencePeriodDto
                        {
                            StartMonth = item.StartMonth ?? 1,
                            StartYear = item.StartYear ?? DateTime.UtcNow.Year,
                            EndMonth = item.IsCurrent ? null : item.EndMonth,
                            EndYear = item.IsCurrent ? null : item.EndYear,
                            IsCurrent = item.IsCurrent
                        },
                        Bullets = item.Bullets
                            .Select(value => value.Trim())
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                            .ToList()
                    })
                    .Where(item => !string.IsNullOrWhiteSpace(item.Title))
                    .ToList(),
                aiPreview.Projects
                    .Select(item => new CandidateProjectDto
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = string.IsNullOrWhiteSpace(item.Name) ? "Project" : item.Name.Trim(),
                        Role = TextNormalizationHelper.NormalizeOptionalText(item.Role),
                        Description = TextNormalizationHelper.NormalizeOptionalText(item.Description),
                        Technologies = item.Technologies
                            .Select(value => value.Trim())
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        Period = new CandidateExperiencePeriodDto
                        {
                            StartMonth = item.StartMonth ?? 1,
                            StartYear = item.StartYear ?? DateTime.UtcNow.Year,
                            EndMonth = item.IsCurrent ? null : item.EndMonth,
                            EndYear = item.IsCurrent ? null : item.EndYear,
                            IsCurrent = item.IsCurrent
                        }
                    })
                    .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                    .ToList(),
                aiPreview.Educations
                    .Select(item => new CandidateEducationDto
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        School = item.School.Trim(),
                        Degree = item.Degree.Trim(),
                        FieldOfStudy = TextNormalizationHelper.NormalizeOptionalText(item.FieldOfStudy),
                        StartYear = item.StartYear,
                        EndYear = item.EndYear,
                        Description = TextNormalizationHelper.NormalizeOptionalText(item.Description)
                    })
                    .Where(item => !string.IsNullOrWhiteSpace(item.School) && !string.IsNullOrWhiteSpace(item.Degree))
                    .ToList(),
                aiPreview.Certifications
                    .Select(item => new CandidateCertificationDto
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = item.Name.Trim(),
                        Issuer = TextNormalizationHelper.NormalizeOptionalText(item.Issuer),
                        IssuedOn = item.IssuedOn,
                        ExpiresOn = item.ExpiresOn,
                        CredentialId = TextNormalizationHelper.NormalizeOptionalText(item.CredentialId),
                        CredentialUrl = TextNormalizationHelper.NormalizeOptionalText(item.CredentialUrl)
                    })
                    .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                    .ToList(),
                aiPreview.Languages
                    .Select(item => new CandidateLanguageDto
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = item.Name.Trim(),
                        Proficiency = string.IsNullOrWhiteSpace(item.Proficiency) ? "Unspecified" : item.Proficiency.Trim()
                    })
                    .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                    .DistinctBy(item => item.Name.ToLowerInvariant())
                    .ToList(),
                aiPreview.Awards,
                aiPreview.Activities),
            Notes = aiPreview.ParserWarnings.Count > 0
                ? aiPreview.ParserWarnings
                : ["AI đã trích xuất dữ liệu từ CV. Hãy kiểm tra trước khi lưu."],
            ExtractedTextPreview = string.Join(Environment.NewLine, NormalizeResumeText(extractedText).Split('\n').Take(40))
        };
    }

    private static List<CandidateProfileSectionDto> BuildSectionsFromParsedPreview(
        IReadOnlyList<CandidateExperienceDto> experiences,
        IReadOnlyList<CandidateProjectDto> projects,
        IReadOnlyList<CandidateEducationDto> educations,
        IReadOnlyList<CandidateCertificationDto> certifications,
        IReadOnlyList<CandidateLanguageDto> languages,
        IReadOnlyList<CandidateResumeAiAwardDto> awards,
        IReadOnlyList<CandidateResumeAiActivityDto> activities)
    {
        List<CandidateProfileSectionDto> sections = [];

        void AddSection(CandidateProfileSectionDto section)
        {
            if (section.Items.Count > 0)
            {
                sections.Add(section);
            }
        }

        AddSection(new CandidateProfileSectionDto
        {
            SectionKey = "experience",
            Title = "Experience",
            SectionType = "Timeline",
            Source = "Parser",
            DisplayOrder = 100,
            Items = experiences.Select((entry, index) => new CandidateProfileSectionItemDto
            {
                Id = entry.Id,
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
            }).ToList()
        });

        AddSection(new CandidateProfileSectionDto
        {
            SectionKey = "projects",
            Title = "Projects",
            SectionType = "Portfolio",
            Source = "Parser",
            DisplayOrder = 200,
            Items = projects.Select((project, index) => new CandidateProfileSectionItemDto
            {
                Id = project.Id,
                ItemType = "Project",
                Title = project.Name,
                Subtitle = project.Role,
                Description = project.Description,
                StartMonth = project.Period.StartMonth,
                StartYear = project.Period.StartYear,
                EndMonth = project.Period.EndMonth,
                EndYear = project.Period.EndYear,
                IsCurrent = project.Period.IsCurrent,
                DisplayOrder = index,
                Tags = project.Technologies
            }).ToList()
        });

        AddSection(new CandidateProfileSectionDto
        {
            SectionKey = "education",
            Title = "Education",
            SectionType = "Education",
            Source = "Parser",
            DisplayOrder = 300,
            Items = educations.Select((education, index) => new CandidateProfileSectionItemDto
            {
                Id = education.Id,
                ItemType = "Education",
                Title = education.School,
                Subtitle = education.Degree,
                Description = education.Description,
                StartYear = education.StartYear,
                EndYear = education.EndYear,
                DisplayOrder = index,
                Attributes = new Dictionary<string, string>
                {
                    ["fieldOfStudy"] = education.FieldOfStudy ?? string.Empty
                }
            }).ToList()
        });

        AddSection(new CandidateProfileSectionDto
        {
            SectionKey = "certifications",
            Title = "Certifications",
            SectionType = "Achievements",
            Source = "Parser",
            DisplayOrder = 400,
            Items = certifications.Select((certification, index) => new CandidateProfileSectionItemDto
            {
                Id = certification.Id,
                ItemType = "Certification",
                Title = certification.Name,
                Organization = certification.Issuer,
                DateLabel = certification.IssuedOn?.ToString("yyyy-MM-dd"),
                DisplayOrder = index,
                Attributes = new Dictionary<string, string>
                {
                    ["expiresOn"] = certification.ExpiresOn?.ToString("yyyy-MM-dd") ?? string.Empty,
                    ["credentialId"] = certification.CredentialId ?? string.Empty,
                    ["credentialUrl"] = certification.CredentialUrl ?? string.Empty
                }
            }).ToList()
        });

        AddSection(new CandidateProfileSectionDto
        {
            SectionKey = "languages",
            Title = "Languages",
            SectionType = "Attributes",
            Source = "Parser",
            DisplayOrder = 500,
            Items = languages.Select((language, index) => new CandidateProfileSectionItemDto
            {
                Id = language.Id,
                ItemType = "Language",
                Title = language.Name,
                Subtitle = language.Proficiency,
                DisplayOrder = index
            }).ToList()
        });

        AddSection(new CandidateProfileSectionDto
        {
            SectionKey = "awards",
            Title = "Vinh danh",
            SectionType = "Achievements",
            Source = "Parser",
            DisplayOrder = 600,
            Items = awards.Select((award, index) => new CandidateProfileSectionItemDto
            {
                Id = Guid.NewGuid().ToString("N"),
                ItemType = "Award",
                Title = award.Name,
                Organization = award.Issuer,
                Description = award.Description,
                StartYear = award.Year,
                DisplayOrder = index
            }).ToList()
        });

        AddSection(new CandidateProfileSectionDto
        {
            SectionKey = "activities",
            Title = "Hoạt động",
            SectionType = "Activities",
            Source = "Parser",
            DisplayOrder = 700,
            Items = activities.Select((activity, index) => new CandidateProfileSectionItemDto
            {
                Id = Guid.NewGuid().ToString("N"),
                ItemType = "Activity",
                Title = activity.Organization,
                Subtitle = activity.Role,
                Description = activity.Description,
                StartYear = activity.StartYear,
                EndYear = activity.EndYear,
                DisplayOrder = index
            }).ToList()
        });

        return sections;
    }

    /// <summary>
    /// Normalizes resume text.
    /// </summary>
    /// <param name="text">The <paramref name="text"/> value.</param>
    /// <returns>The resulting string value.</returns>
    public string NormalizeResumeText(string text)
    {
        string normalized = TextNormalizationHelper.RemoveInvalidDatabaseCharacters(text);
        normalized = normalized.Replace("\r\n", "\n").Replace('\r', '\n');
        normalized = Regex.Replace(normalized, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.Trim();
    }

    public bool HasUsableResumeText(string? extractedText)
    {
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            return false;
        }

        return extractedText.Trim().Length >= MinimumResumeTextLength;
    }

    public List<string> NormalizeParserWarnings(IEnumerable<string>? warnings)
    {
        return (warnings ?? [])
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Extracts resume sections.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static Dictionary<string, List<string>> ExtractResumeSections(List<string> lines)
    {
        Dictionary<string, List<string>> sections = new(StringComparer.OrdinalIgnoreCase);
        string currentSection = "general";
        sections[currentSection] = [];

        foreach (string line in lines)
        {
            string? detectedSection = DetectSectionKey(line);
            if (detectedSection != null)
            {
                currentSection = detectedSection;
                if (!sections.ContainsKey(currentSection))
                {
                    sections[currentSection] = [];
                }

                continue;
            }

            sections[currentSection].Add(line);
        }

        return sections;
    }

    /// <summary>
    /// Executes the detect section key operation.
    /// </summary>
    /// <param name="line">The <paramref name="line"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? DetectSectionKey(string line)
    {
        string normalized = line.Trim().Trim(':').ToLowerInvariant();
        if (normalized is "experience" or "work experience" or "employment history" or "professional experience")
        {
            return "experience";
        }

        if (normalized is "projects" or "personal projects" or "project experience")
        {
            return "projects";
        }

        if (normalized is "education" or "academic background")
        {
            return "education";
        }

        if (normalized is "skills" or "technical skills" or "core skills")
        {
            return "skills";
        }

        if (normalized is "certifications" or "licenses" or "awards")
        {
            return "certifications";
        }

        if (normalized is "languages" or "language")
        {
            return "languages";
        }

        if (normalized is "summary" or "profile" or "objective" or "about")
        {
            return "summary";
        }

        return null;
    }

    /// <summary>
    /// Extracts email.
    /// </summary>
    /// <param name="text">The <paramref name="text"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? ExtractEmail(string text)
    {
        Match match = EmailExtractorPattern.Match(text);
        return match.Success ? match.Groups["email"].Value.Trim() : null;
    }

    /// <summary>
    /// Extracts phone.
    /// </summary>
    /// <param name="text">The <paramref name="text"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? ExtractPhone(string text)
    {
        Match match = PhoneExtractorPattern.Match(text);
        if (!match.Success)
        {
            return null;
        }

        string digits = Regex.Replace(match.Groups["phone"].Value, @"[^\d+]", string.Empty);
        return digits.StartsWith("+84", StringComparison.Ordinal) ? "0" + digits[3..] : digits;
    }

    /// <summary>
    /// Extracts url.
    /// </summary>
    /// <param name="text">The <paramref name="text"/> value.</param>
    /// <param name="hostKeyword">The <paramref name="hostKeyword"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? ExtractUrl(string text, string hostKeyword)
    {
        Match match = UrlPattern.Matches(text)
            .FirstOrDefault(item => item.Value.Contains(hostKeyword, StringComparison.OrdinalIgnoreCase));
        return match?.Value.Trim();
    }

    /// <summary>
    /// Extracts candidate name.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? ExtractCandidateName(List<string> lines)
    {
        return lines
            .Take(6)
            .Select(line => line.Trim(' ', '-', '*', '\t'))
            .FirstOrDefault(line =>
                line.Length >= 4
                && line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 5
                && !line.Contains('@')
                && !line.Contains("linkedin", StringComparison.OrdinalIgnoreCase)
                && !line.Contains("github", StringComparison.OrdinalIgnoreCase)
                && !line.Any(char.IsDigit));
    }

    /// <summary>
    /// Extracts headline.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <param name="name">The <paramref name="name"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? ExtractHeadline(List<string> lines, string? name)
    {
        return lines
            .SkipWhile(line => string.Equals(line, name, StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault(line =>
                line.Length >= 4
                && !line.Contains('@')
                && !line.Contains("http", StringComparison.OrdinalIgnoreCase)
                && !YearPattern.IsMatch(line));
    }

    /// <summary>
    /// Extracts location.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? ExtractLocation(List<string> lines)
    {
        return lines.FirstOrDefault(line =>
            line.Contains("location", StringComparison.OrdinalIgnoreCase)
            || line.Contains("address", StringComparison.OrdinalIgnoreCase)
            || line.Contains("ho chi minh", StringComparison.OrdinalIgnoreCase)
            || line.Contains("hanoi", StringComparison.OrdinalIgnoreCase)
            || line.Contains("da nang", StringComparison.OrdinalIgnoreCase)
            || line.Contains("vietnam", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extracts summary.
    /// </summary>
    /// <param name="sections">The <paramref name="sections"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? ExtractSummary(Dictionary<string, List<string>> sections)
    {
        if (!sections.TryGetValue("summary", out List<string>? summaryLines) || summaryLines.Count == 0)
        {
            return null;
        }

        return string.Join(" ", summaryLines.Take(4));
    }

    /// <summary>
    /// Executes the match skills operation.
    /// </summary>
    /// <param name="text">The <paramref name="text"/> value.</param>
    /// <param name="allSkills">The <paramref name="allSkills"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<Skill> MatchSkills(string text, IReadOnlyList<Skill> allSkills)
    {
        List<Skill> matches = [];
        foreach (Skill skill in allSkills.OrderByDescending(item => item.Name.Length))
        {
            string escapedSkillName = Regex.Escape(skill.Name);
            if (Regex.IsMatch(text, $@"(?<!\w){escapedSkillName}(?!\w)", RegexOptions.IgnoreCase))
            {
                matches.Add(skill);
            }
        }

        return matches
            .DistinctBy(skill => skill.Id)
            .Take(20)
            .ToList();
    }

    /// <summary>
    /// Extracts years of experience for skill.
    /// </summary>
    /// <param name="text">The <paramref name="text"/> value.</param>
    /// <param name="skillName">The <paramref name="skillName"/> value.</param>
    /// <returns>The operation result.</returns>
    private static decimal? ExtractYearsOfExperienceForSkill(string text, string skillName)
    {
        string escapedSkillName = Regex.Escape(skillName);
        string[] patterns =
        [
            $@"{escapedSkillName}[^\n\.]{{0,32}}?(?<years>\d+(?:\.\d+)?)\s*(?:\+)?\s*(?:years?|yrs?)",
            $@"(?<years>\d+(?:\.\d+)?)\s*(?:\+)?\s*(?:years?|yrs?)[^\n\.]{{0,32}}?{escapedSkillName}",
            $@"{escapedSkillName}\s*[-:()]*\s*(?<years>\d+(?:\.\d+)?)"
        ];

        foreach (string pattern in patterns)
        {
            Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && decimal.TryParse(match.Groups["years"].Value, out decimal parsedYears))
            {
                return parsedYears;
            }
        }

        return null;
    }

    /// <summary>
    /// Executes the split into chunks operation.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<List<string>> SplitIntoChunks(List<string> lines)
    {
        List<List<string>> chunks = [];
        List<string> currentChunk = [];
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (currentChunk.Count > 0)
                {
                    chunks.Add([.. currentChunk]);
                    currentChunk.Clear();
                }

                continue;
            }

            bool startsNewChunk = currentChunk.Count > 0 && ContainsDateRange(line);
            if (startsNewChunk)
            {
                chunks.Add([.. currentChunk]);
                currentChunk.Clear();
            }

            currentChunk.Add(line.Trim());
        }

        if (currentChunk.Count > 0)
        {
            chunks.Add(currentChunk);
        }

        return chunks;
    }

    /// <summary>
    /// Executes the contains date range operation.
    /// </summary>
    /// <param name="line">The <paramref name="line"/> value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    private static bool ContainsDateRange(string line)
    {
        return Regex.IsMatch(line, @"(?:(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\s+)?(?:19|20)\d{2}\s*[-–]\s*(?:(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\s+)?(?:(?:19|20)\d{2}|present|current)", RegexOptions.IgnoreCase)
            || Regex.IsMatch(line, @"\b(19|20)\d{2}\b");
    }

    /// <summary>
    /// Parses period from chunk.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static CandidateExperiencePeriodDto ParsePeriodFromChunk(IEnumerable<string> lines)
    {
        string combined = string.Join(" ", lines);
        List<int> years = YearPattern.Matches(combined)
            .Select(match => int.TryParse(match.Value, out int year) ? year : 0)
            .Where(year => year > 0)
            .Take(2)
            .ToList();

        int startYear = years.FirstOrDefault();
        int? endYear = years.Skip(1).FirstOrDefault();
        bool isCurrent = Regex.IsMatch(combined, @"\b(present|current|now)\b", RegexOptions.IgnoreCase);

        return new CandidateExperiencePeriodDto
        {
            StartMonth = ExtractMonthNumber(combined) ?? 1,
            StartYear = startYear == 0 ? DateTime.UtcNow.Year : startYear,
            EndMonth = isCurrent ? null : ExtractMonthNumber(combined, last: true),
            EndYear = isCurrent || endYear == 0 ? null : endYear,
            IsCurrent = isCurrent
        };
    }

    /// <summary>
    /// Extracts month number.
    /// </summary>
    /// <param name="value">The <paramref name="value"/> value.</param>
    /// <param name="last">The <paramref name="last"/> value.</param>
    /// <returns>The operation result.</returns>
    private static int? ExtractMonthNumber(string value, bool last = false)
    {
        string[] monthTokens = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "sept", "oct", "nov", "dec"];
        List<int> monthIndexes = [];
        foreach ((string token, int index) in monthTokens.Select((token, index) => (token, index)))
        {
            if (Regex.IsMatch(value, $@"\b{token}[a-z]*\b", RegexOptions.IgnoreCase))
            {
                monthIndexes.Add(index + 1);
            }
        }

        if (monthIndexes.Count == 0)
        {
            return null;
        }

        return last ? monthIndexes.Last() : monthIndexes.First();
    }

    /// <summary>
    /// Parses experience entries.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <param name="fallbackTitle">The <paramref name="fallbackTitle"/> value.</param>
    /// <param name="defaultCompany">The <paramref name="defaultCompany"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<CandidateExperienceDto> ParseExperienceEntries(List<string> lines, string fallbackTitle, string defaultCompany)
    {
        return SplitIntoChunks(lines)
            .Select(chunk =>
            {
                CandidateExperiencePeriodDto period = ParsePeriodFromChunk(chunk);
                List<string> contentLines = chunk.Where(line => !ContainsDateRange(line)).ToList();
                string firstLine = contentLines.FirstOrDefault() ?? fallbackTitle;
                string secondLine = contentLines.Skip(1).FirstOrDefault() ?? defaultCompany;
                List<string> bulletLines = contentLines.Skip(2)
                    .Select(line => line.TrimStart('-', '*', '•', ' '))
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();

                if (firstLine.Contains(" at ", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = firstLine.Split(" at ", 2, StringSplitOptions.TrimEntries);
                    firstLine = parts[0];
                    secondLine = parts.Length > 1 ? parts[1] : secondLine;
                }

                return new CandidateExperienceDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = firstLine,
                    Company = secondLine,
                    Period = period,
                    Bullets = bulletLines
                };
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.Company))
            .Take(8)
            .ToList();
    }

    /// <summary>
    /// Parses projects.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<CandidateProjectDto> ParseProjects(List<string> lines)
    {
        return SplitIntoChunks(lines)
            .Select(chunk =>
            {
                CandidateExperiencePeriodDto period = ParsePeriodFromChunk(chunk);
                List<string> contentLines = chunk.Where(line => !ContainsDateRange(line)).ToList();
                string name = contentLines.FirstOrDefault() ?? "Project";
                string? role = contentLines.Skip(1).FirstOrDefault();
                string? description = string.Join(" ", contentLines.Skip(2)).Trim();
                List<string> technologies = Regex.Split(string.Join(" ", chunk), @"[,/|]")
                    .Select(value => value.Trim())
                    .Where(value => value.Length > 1 && value.Length <= 30)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(8)
                    .ToList();

                return new CandidateProjectDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Role = role,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description,
                    Technologies = technologies,
                    Period = period
                };
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Take(6)
            .ToList();
    }

    /// <summary>
    /// Parses educations.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<CandidateEducationDto> ParseEducations(List<string> lines)
    {
        return SplitIntoChunks(lines)
            .Select(chunk =>
            {
                string school = chunk.FirstOrDefault() ?? string.Empty;
                string degree = chunk.Skip(1).FirstOrDefault() ?? "Education";
                List<int> years = chunk
                    .SelectMany(line => YearPattern.Matches(line).Select(match => int.Parse(match.Value)))
                    .Take(2)
                    .ToList();

                return new CandidateEducationDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    School = school,
                    Degree = degree,
                    FieldOfStudy = null,
                    StartYear = years.FirstOrDefault() == 0 ? null : years.FirstOrDefault(),
                    EndYear = years.Skip(1).FirstOrDefault() == 0 ? null : years.Skip(1).FirstOrDefault(),
                    Description = chunk.Count > 2 ? string.Join(" ", chunk.Skip(2)) : null
                };
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.School) && !string.IsNullOrWhiteSpace(item.Degree))
            .Take(5)
            .ToList();
    }

    /// <summary>
    /// Parses certifications.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<CandidateCertificationDto> ParseCertifications(List<string> lines)
    {
        return SplitIntoChunks(lines)
            .Select(chunk => new CandidateCertificationDto
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = chunk.FirstOrDefault() ?? string.Empty,
                Issuer = chunk.Skip(1).FirstOrDefault(),
                IssuedOn = null,
                ExpiresOn = null,
                CredentialId = null,
                CredentialUrl = null
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Take(6)
            .ToList();
    }

    /// <summary>
    /// Parses languages.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<CandidateLanguageDto> ParseLanguages(List<string> lines)
    {
        List<string> proficiencyKeywords = ["native", "fluent", "advanced", "intermediate", "basic", "professional", "business"];
        return lines
            .SelectMany(line => Regex.Split(line, @"[,;|]"))
            .Select(token => token.Trim())
            .Where(token => token.Length > 1)
            .Select(token =>
            {
                string proficiency = proficiencyKeywords
                    .FirstOrDefault(keyword => token.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    ?? "Unspecified";
                string languageName = Regex.Replace(token, @"\((.*?)\)|\b(native|fluent|advanced|intermediate|basic|professional|business)\b", string.Empty, RegexOptions.IgnoreCase).Trim(' ', '-', '–', ':');
                return new CandidateLanguageDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = languageName,
                    Proficiency = proficiency
                };
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .DistinctBy(item => item.Name.ToLowerInvariant())
            .Take(8)
            .ToList();
    }
}

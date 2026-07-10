using RecruitPro.Application.DTOs.Response;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IServices;

/// <summary>
/// Resume text-extraction and heuristic preview-building collaborator. Extracted from CandidateService so
/// the god-service keeps orchestration/persistence while all pure text transforms live here.
/// </summary>
public interface IResumeParsingService
{
    /// <summary>Extracts raw text from a PDF/DOCX/TXT resume stream. Callers still normalize the result.</summary>
    Task<string> ExtractResumeTextAsync(Stream resumeStream, string fileName);

    /// <summary>Normalizes resume text (line endings, whitespace, invalid DB chars).</summary>
    string NormalizeResumeText(string text);

    /// <summary>True when the extracted text is long enough to be worth parsing.</summary>
    bool HasUsableResumeText(string? extractedText);

    /// <summary>Trims, de-blanks and de-duplicates parser warning strings.</summary>
    List<string> NormalizeParserWarnings(IEnumerable<string>? warnings);

    /// <summary>Builds a heuristic (non-AI) parse preview from extracted resume text.</summary>
    CandidateResumeParseResponseDto BuildResumeParsePreview(string extractedText, IReadOnlyList<Skill> allSkills);

    /// <summary>Builds a parse preview from an AI parse result, reconciling skills against the catalog.</summary>
    CandidateResumeParseResponseDto BuildResumeParsePreviewFromAi(
        CandidateResumeAiParseDto aiPreview,
        string extractedText,
        IReadOnlyList<Skill> allSkills,
        string? modelName);
}

namespace RecruitPro.Application.Configurations;

public class AiProviderSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai";
    public string Model { get; set; } = "gemini-3.1-flash-lite";
    public string EmbeddingModel { get; set; } = "gemini-embedding-002";
    public int MaxCandidatesForAi { get; set; } = 50;
    public int MaxResumeCharsPerCandidate { get; set; } = 6000;
    public int MaxResumeParseChars { get; set; } = 12000;
    public int RequestTimeoutSeconds { get; set; } = 180;
    public bool Enabled { get; set; } = true;

    // v2: deterministic ranking is the source of truth for candidate ORDER. By default the provider
    // may only enrich the Vietnamese explanation (summary/evidence/strengths/gaps) — it must not
    // reorder candidates. Set true only if the business explicitly wants provider-driven ordering.
    public bool AllowProviderReordering { get; set; } = false;
}

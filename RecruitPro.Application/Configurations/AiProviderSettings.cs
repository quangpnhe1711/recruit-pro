namespace RecruitPro.Application.Configurations;

public class AiProviderSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai";
    public string Model { get; set; } = "gemini-2.0-flash";
    public string EmbeddingModel { get; set; } = "text-embedding-004";
    public int MaxCandidatesForAi { get; set; } = 50;
    public int MaxResumeCharsPerCandidate { get; set; } = 6000;
    public int MaxResumeParseChars { get; set; } = 12000;
    public int RequestTimeoutSeconds { get; set; } = 180;
    public bool Enabled { get; set; } = true;
}

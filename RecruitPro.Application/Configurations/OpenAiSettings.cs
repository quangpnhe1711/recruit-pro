namespace RecruitPro.Application.Configurations;

public class OpenAiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-4.1-mini";
    public int MaxCandidatesForAi { get; set; } = 50;
    public int MaxResumeCharsPerCandidate { get; set; } = 6000;
    public bool Enabled { get; set; } = true;
}

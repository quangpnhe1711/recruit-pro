using System.Text.Json.Serialization;

namespace RecruitPro.Application.DTOs.Response
{
    public class ApplicationCandidateSummaryDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("avatarUrl")]
        public string? AvatarUrl { get; set; }

        [JsonPropertyName("currentPosition")]
        public string? CurrentPosition { get; set; }
    }

    public class ApplicationJobSummaryDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("department")]
        public DepartmentDto Department { get; set; } = new();
    }

    public class DepartmentDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    public class ApplicationListItemDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("candidate")]
        public ApplicationCandidateSummaryDto Candidate { get; set; } = new();

        [JsonPropertyName("job")]
        public ApplicationJobSummaryDto Job { get; set; } = new();

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("appliedAt")]
        public DateTime AppliedAt { get; set; }

        [JsonPropertyName("reviewedBy")]
        public UserDto? ReviewedBy { get; set; }

        [JsonPropertyName("nextStep")]
        public string? NextStep { get; set; }
    }
}

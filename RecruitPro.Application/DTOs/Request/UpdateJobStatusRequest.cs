using System.Text.Json.Serialization;

namespace RecruitPro.Application.DTOs.Request
{
    public class UpdateJobStatusRequest
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }
}

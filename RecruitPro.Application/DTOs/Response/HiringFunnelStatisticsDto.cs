using System.Text.Json.Serialization;

namespace RecruitPro.Application.DTOs.Response
{
    public class HiringFunnelStageDto
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; } = string.Empty;
    }

    public class HiringFunnelStatisticsDto
    {
        [JsonPropertyName("applied")]
        public int Applied { get; set; }

        [JsonPropertyName("screening")]
        public int Screening { get; set; }

        [JsonPropertyName("interview")]
        public int Interview { get; set; }

        [JsonPropertyName("offer")]
        public int Offer { get; set; }

        [JsonPropertyName("hired")]
        public int Hired { get; set; }
    }
}

using System.Text.Json.Serialization;

namespace RecruitPro.Application.DTOs.Response
{
    public class PaginatedResponseDto<T>
    {
        [JsonPropertyName("items")]
        public List<T> Items { get; set; } = new();

        [JsonPropertyName("currentPage")]
        public int CurrentPage { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("totalItems")]
        public int TotalItems { get; set; }

        [JsonPropertyName("totalPages")]
        public int TotalPages => (TotalItems + PageSize - 1) / PageSize;
    }
}

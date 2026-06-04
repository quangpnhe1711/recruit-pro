namespace RecruitPro.Application.DTOs.Response;

public class SalaryRangeDto
{
    public string Label { get; set; } = string.Empty;
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
}

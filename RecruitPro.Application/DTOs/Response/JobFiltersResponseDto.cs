namespace RecruitPro.Application.DTOs.Response;

public class JobFiltersResponseDto
{
    public List<SalaryRangeDto> SalaryRanges { get; set; } = [];
    public List<string> EmploymentTypes { get; set; } = [];
    public List<string> Skills { get; set; } = [];
}

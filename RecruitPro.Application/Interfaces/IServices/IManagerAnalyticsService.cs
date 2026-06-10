using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices;

public interface IManagerAnalyticsService
{
    Task<ApiResponse<ManagerRecruitmentAnalyticsDto>> GetRecruitmentAnalyticsAsync();
}

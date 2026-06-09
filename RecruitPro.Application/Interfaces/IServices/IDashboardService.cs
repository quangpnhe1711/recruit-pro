using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices;

public interface IDashboardService
{
    Task<ApiResponse<CandidateDashboardDto>> GetCandidateDashboardAsync(Guid userId);
    Task<ApiResponse<HrDashboardDto>> GetHrDashboardAsync();
}

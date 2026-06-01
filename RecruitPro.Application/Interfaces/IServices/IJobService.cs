using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices
{
    public interface IJobService
    {
        public Task<ApiResponse<JobsListingResponseDto>> GetJobsAsync(int currentPage = 1, int pageSize = 10);
    }
}

using AutoMapper;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Services
{
    public class JobService : IJobService
    {
        private readonly IJobRepository _jobRepository;
        private readonly IMapper _mapper;

        public JobService(IJobRepository jobRepository, IMapper mapper)
        {
            _jobRepository = jobRepository;
            _mapper = mapper;
        }

        public async Task<ApiResponse<JobsListingResponseDto>> GetAllJobsAsync()
        {
            IReadOnlyList<Job> jobs = await _jobRepository.GetAllApprovedAsync();
            List<JobCardDto> jobCards = _mapper.Map<List<JobCardDto>>(jobs);

            var response = new JobsListingResponseDto
            {
                Jobs = jobCards,
                Total = jobCards.Count,
                Page = 1,
                Limit = jobCards.Count
            };

            return ApiResponse<JobsListingResponseDto>.Ok(response);
        }
    }
}

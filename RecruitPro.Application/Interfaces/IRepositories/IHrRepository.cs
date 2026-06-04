using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using JobApplication = RecruitPro.Domain.Entities.Application;

namespace RecruitPro.Application.Interfaces.IRepositories;

public interface IHrRepository
{
    Task<Interview?> GetNextInterviewAsync(DateTime fromDate);
    Task<IReadOnlyList<JobApplication>> GetRecentApplicationsAsync(int take);
    Task<IReadOnlyList<Job>> GetPendingApprovalJobsAsync(int take);
    Task<int> CountApprovedJobsAsync();
    Task<int> CountApplicationsAsync();
    Task<int> CountInterviewsOnDateAsync(DateTime date);
    Task<(IReadOnlyList<Job> Jobs, int Total)> GetJobsAsync(string? department, JobStatus? status, int page, int pageSize);
    Task<Department?> GetDepartmentByNameAsync(string departmentName);
    Task<Job?> GetJobByIdAsync(Guid jobId);
    Task AddJobAsync(Job job);
    Task DeleteJobAsync(Job job);
    Task<(IReadOnlyList<CandidateProfile> Candidates, int Total)> GetCandidatesAsync(int page, int pageSize, string? keyword);
    Task<(IReadOnlyList<JobApplication> Applications, int Total)> GetApplicationsAsync(int page, int pageSize, string? keyword, string? department, ApplicationStatus? status);
    Task<JobApplication?> GetApplicationByIdAsync(Guid applicationId);
    Task<(IReadOnlyList<Interview> Interviews, int Total)> GetInterviewsAsync(int page, int pageSize, string? keyword, InterviewStatus? status, DateTime? startDate, DateTime? endDate);
    Task<Interview?> GetInterviewByIdAsync(Guid interviewId);
    Task<CandidateProfile?> GetFirstCandidateAsync();
    Task<IReadOnlyList<User>> GetInterviewersAsync(int take);
    Task AddInterviewAsync(Interview interview);
    Task DeleteInterviewAsync(Interview interview);
}

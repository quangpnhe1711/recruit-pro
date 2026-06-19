using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.DTOs.Response.Copilot;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Constants;
using RecruitPro.Domain.Entities;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Repositories;

public class CopilotRepository : ICopilotRepository
{
    private readonly AppDbContext _context;

    public CopilotRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CopilotJobOptionDto>> GetJobOptionsAsync()
    {
        return await _context.Jobs
            .AsNoTracking()
            .Where(job => !Constants.NOT_SHOW_JOB_STATUS.Contains(job.Status))
            .OrderByDescending(job => job.CreatedAt)
            .Select(job => new CopilotJobOptionDto
            {
                JobId = job.Id,
                Title = job.Title,
                Status = job.Status.ToString(),
                ApplicationCount = job.Applications.Count
            })
            .ToListAsync();
    }

    public async Task<CopilotCandidatePoolDto?> GetCandidatePoolAsync(Guid jobId)
    {
        CopilotJobContextDto? jobContext = await _context.Jobs
            .AsNoTracking()
            .Where(job => job.Id == jobId)
            .Select(job => new CopilotJobContextDto
            {
                JobId = job.Id,
                Title = job.Title,
                Description = job.Description,
                Requirements = SplitText(job.Requirements),
                RequiredSkills = job.JobSkills
                    .Where(jobSkill => jobSkill.IsRequired == true)
                    .Select(jobSkill => jobSkill.Skill.Name)
                    .OrderBy(name => name)
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (jobContext is null)
        {
            return null;
        }

        List<CopilotCandidateDto> candidates = await _context.Applications
            .AsNoTracking()
            .Where(application => application.JobId == jobId)
            .OrderByDescending(application => application.AppliedAt)
            .Select(application => new CopilotCandidateDto
            {
                CandidateUserId = application.UserId,
                ApplicationId = application.Id,
                FullName = application.User.FullName,
                Education = application.User.CandidateProfile != null ? application.User.CandidateProfile.Education : null,
                ExperienceYears = application.User.CandidateProfile != null && application.User.CandidateProfile.ExperienceYears.HasValue
                    ? application.User.CandidateProfile.ExperienceYears.Value
                    : 0,
                Skills = application.User.CandidateProfile != null
                    ? application.User.CandidateProfile.CandidateSkills.Select(skill => skill.Skill.Name).OrderBy(name => name).ToList()
                    : new List<string>(),
                CvSummary = application.User.CandidateProfile != null
                    ? (application.User.CandidateProfile.Bio ?? application.User.CandidateProfile.CurrentPosition ?? string.Empty)
                    : string.Empty,
                ResumeUrl = application.User.CandidateProfile != null ? application.User.CandidateProfile.ResumeUrl : null
            })
            .ToListAsync();

        return new CopilotCandidatePoolDto
        {
            Job = jobContext,
            Candidates = candidates
        };
    }

    public Task<CopilotConversation?> GetConversationAsync(Guid conversationId)
    {
        return _context.CopilotConversations
            .FirstOrDefaultAsync(conversation => conversation.Id == conversationId);
    }

    public Task<CopilotConversation?> GetConversationWithDetailsAsync(Guid conversationId)
    {
        return _context.CopilotConversations
            .Include(conversation => conversation.Messages.OrderBy(message => message.SequenceNo))
            .Include(conversation => conversation.RankingSessions)
                .ThenInclude(session => session.Results)
            .FirstOrDefaultAsync(conversation => conversation.Id == conversationId);
    }

    public Task<CopilotConversation?> GetLatestConversationAsync(Guid jobId, Guid userId)
    {
        return _context.CopilotConversations
            .OrderByDescending(conversation => conversation.CreatedAt)
            .FirstOrDefaultAsync(conversation => conversation.JobId == jobId && conversation.UserId == userId);
    }

    public Task<CopilotRankingSession?> GetRankingSessionAsync(Guid rankingSessionId)
    {
        return _context.CopilotRankingSessions
            .Include(session => session.Results)
                .ThenInclude(result => result.Application)
                    .ThenInclude(application => application.User)
            .FirstOrDefaultAsync(session => session.Id == rankingSessionId);
    }

    public async Task<IReadOnlyList<CopilotSavedRule>> GetSavedRulesAsync(Guid jobId, Guid userId)
    {
        return await _context.CopilotSavedRules
            .AsNoTracking()
            .Where(rule => rule.JobId == jobId && rule.UserId == userId && !rule.IsDeleted)
            .OrderByDescending(rule => rule.IsActive)
            .ThenByDescending(rule => rule.UpdatedAt)
            .ToListAsync();
    }

    public Task<CopilotSavedRule?> GetSavedRuleAsync(Guid ruleId, Guid userId)
    {
        return _context.CopilotSavedRules
            .FirstOrDefaultAsync(rule => rule.Id == ruleId && rule.UserId == userId && !rule.IsDeleted);
    }

    public async Task<int> GetNextMessageSequenceAsync(Guid conversationId)
    {
        int currentMax = await _context.CopilotMessages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .Select(message => (int?)message.SequenceNo)
            .MaxAsync() ?? 0;

        return currentMax + 1;
    }

    public async Task AddConversationAsync(CopilotConversation conversation)
    {
        await _context.CopilotConversations.AddAsync(conversation);
    }

    public async Task AddMessageAsync(CopilotMessage message)
    {
        await _context.CopilotMessages.AddAsync(message);
    }

    public async Task AddRankingSessionAsync(CopilotRankingSession session)
    {
        await _context.CopilotRankingSessions.AddAsync(session);
    }

    public async Task AddSavedRuleAsync(CopilotSavedRule rule)
    {
        await _context.CopilotSavedRules.AddAsync(rule);
    }

    private static IReadOnlyList<string> SplitText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(new[] { "\r\n", "\n", ";", "," }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }
}

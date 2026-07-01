using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Response.Copilot;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Constants;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Repositories;

public class CopilotRepository : ICopilotRepository
{
    private readonly AppDbContext _context;

    public CopilotRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CopilotJobOptionDto>> GetJobOptionsAsync(Guid callerUserId)
    {
        return await _context.Jobs
            .AsNoTracking()
            .Where(job => !Constants.NOT_SHOW_JOB_STATUS.Contains(job.Status))
            // Ownership scope (mirrors OwnershipScope.CanAccessJob / JobRepository.GetPagedAsync): the
            // caller only sees jobs they created, recruit, or head the department of. Keeps the picker in
            // sync with the candidate-pool endpoint so a listed job never 403s when opened.
            .Where(job =>
                job.CreatedBy == callerUserId
                || job.RecruiterId == callerUserId
                || (job.Department != null && job.Department.HeadUserId == callerUserId))
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

        // v2: the Copilot is a CV-screening tool. Its candidate pool only contains applications
        // currently in the Screening stage — candidates already at ManagerReview (Head Review),
        // Interview, Offer, Hired or a closed state are excluded, so passing a CV to Head Review makes
        // that candidate leave the pool and the ranking list on refresh (v2 §6/§15).
        List<RecruitPro.Domain.Entities.Application> applications = await _context.Applications
            .AsNoTracking()
            .Where(application => application.JobId == jobId && application.Status == ApplicationStatus.Screening)
            .OrderByDescending(application => application.AppliedAt)
            .Include(application => application.User)
                .ThenInclude(user => user.CandidateProfile!)
                    .ThenInclude(profile => profile.CandidateSkills)
                        .ThenInclude(candidateSkill => candidateSkill.Skill)
            .Include(application => application.User)
                .ThenInclude(user => user.CandidateProfile!)
                    .ThenInclude(profile => profile.Projects)
            .Include(application => application.User)
                .ThenInclude(user => user.CandidateProfile!)
                    .ThenInclude(profile => profile.Sections)
                        .ThenInclude(section => section.Items)
            .Include(application => application.User)
                .ThenInclude(user => user.CandidateProfile!)
                    .ThenInclude(profile => profile.Resumes)
            .ToListAsync();

        List<CopilotCandidateDto> candidates = applications.Select(application =>
        {
            CandidateProfile? profile = application.User.CandidateProfile;
            CandidateResume? currentResume = profile?.Resumes
                .OrderByDescending(item => item.Version)
                .FirstOrDefault(item => item.IsCurrent);

            return new CopilotCandidateDto
            {
                CandidateUserId = application.UserId,
                ApplicationId = application.Id,
                FullName = application.User.FullName,
                Education = profile?.EducationRecordsJson ?? profile?.Education,
                ExperienceYears = profile?.ExperienceYears ?? 0,
                Skills = profile?.CandidateSkills
                    .Where(skill => skill.Skill != null)
                    .Select(skill => skill.Skill.Name)
                    .OrderBy(name => name)
                    .ToList() ?? [],
                CvSummary = profile == null ? string.Empty : CandidateProfileSectionHelper.BuildStructuredNarrative(profile),
                ResumeUrl = currentResume?.StorageKey,
                Status = application.Status.ToString()
            };
        }).ToList();

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

    public Task<CopilotRankingSession?> GetLatestMatchingRankingSessionAsync(Guid jobId, Guid userId, string effectivePayloadHash)
    {
        if (string.IsNullOrWhiteSpace(effectivePayloadHash))
        {
            return Task.FromResult<CopilotRankingSession?>(null);
        }

        return _context.CopilotRankingSessions
            .Include(session => session.Results)
                .ThenInclude(result => result.Application)
                    .ThenInclude(application => application.User)
            .Where(session => session.JobId == jobId
                && session.UserId == userId
                && session.InputHash == effectivePayloadHash)
            .OrderByDescending(session => session.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public Task<CopilotRankingSession?> GetLatestRankingSessionForJobAsync(Guid jobId, Guid userId)
    {
        return _context.CopilotRankingSessions
            .Include(session => session.Results)
                .ThenInclude(result => result.Application)
                    .ThenInclude(application => application.User)
            .Where(session => session.JobId == jobId && session.UserId == userId)
            .OrderByDescending(session => session.CreatedAt)
            .FirstOrDefaultAsync();
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

    public async Task<IReadOnlyList<CopilotPromptTemplate>> GetPromptTemplatesAsync(Guid ownerUserId)
    {
        return await _context.CopilotPromptTemplates
            .AsNoTracking()
            .Where(template => template.OwnerUserId == ownerUserId)
            .OrderByDescending(template => template.IsActive)
            .ThenBy(template => template.TemplateType)
            .ThenBy(template => template.Name)
            .ToListAsync();
    }

    public Task<CandidateFitAnalysis?> GetLatestFitAnalysisAsync(Guid applicationId)
    {
        return _context.CandidateFitAnalyses
            .AsNoTracking()
            .Include(analysis => analysis.CandidateUser)
            .OrderByDescending(analysis => analysis.CreatedAt)
            .FirstOrDefaultAsync(analysis => analysis.ApplicationId == applicationId);
    }

    public async Task<IReadOnlyList<CopilotGeneratedArtifact>> GetGeneratedArtifactsAsync(
        Guid ownerUserId,
        Guid? jobId,
        Guid? applicationId,
        string? artifactType,
        int take)
    {
        IQueryable<CopilotGeneratedArtifact> query = _context.CopilotGeneratedArtifacts
            .AsNoTracking()
            .Where(artifact => artifact.OwnerUserId == ownerUserId);

        if (jobId.HasValue)
        {
            query = query.Where(artifact => artifact.JobId == jobId.Value);
        }

        if (applicationId.HasValue)
        {
            query = query.Where(artifact => artifact.ApplicationId == applicationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(artifactType))
        {
            string normalizedType = artifactType.Trim();
            query = query.Where(artifact => artifact.ArtifactType == normalizedType);
        }

        return await query
            .OrderByDescending(artifact => artifact.CreatedAt)
            .Take(Math.Clamp(take, 1, 50))
            .ToListAsync();
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

    public async Task AddPromptTemplateAsync(CopilotPromptTemplate template)
    {
        await _context.CopilotPromptTemplates.AddAsync(template);
    }

    public async Task AddFitAnalysisAsync(CandidateFitAnalysis analysis)
    {
        await _context.CandidateFitAnalyses.AddAsync(analysis);
    }

    public async Task AddGeneratedArtifactAsync(CopilotGeneratedArtifact artifact)
    {
        await _context.CopilotGeneratedArtifacts.AddAsync(artifact);
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

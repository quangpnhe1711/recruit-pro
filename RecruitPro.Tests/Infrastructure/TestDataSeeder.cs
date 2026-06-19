using Microsoft.EntityFrameworkCore;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Tests.Infrastructure;

public static class TestDataSeeder
{
    public static readonly Guid CandidateRoleId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid HrRoleId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid ManagerRoleId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid CandidateUserId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid HrUserId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid ManagerUserId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly Guid DepartmentId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid DotNetSkillId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    public static readonly Guid SqlSkillId = Guid.Parse("40000000-0000-0000-0000-000000000002");
    public static readonly Guid CandidateProfileId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    public static readonly Guid ApprovedJobId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    public static readonly Guid PendingJobId = Guid.Parse("60000000-0000-0000-0000-000000000002");
    public static readonly Guid ApplicationId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    public static readonly Guid ResumeId = Guid.Parse("80000000-0000-0000-0000-000000000001");
    public static readonly Guid InterviewId = Guid.Parse("90000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(AppDbContext db)
    {
        DateTime now = TimestampNow();

        if (await db.Users.AnyAsync())
        {
            return;
        }

        Role candidateRole = new() { Id = CandidateRoleId, Name = "Candidate" };
        Role hrRole = new() { Id = HrRoleId, Name = "HR" };
        Role managerRole = new() { Id = ManagerRoleId, Name = "Manager" };

        User candidate = new()
        {
            Id = CandidateUserId,
            Email = "candidate@recruitpro.test",
            FullName = "Candidate User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass@123"),
            Phone = "0900000001",
            CreatedAt = now,
            UpdatedAt = now
        };

        User hr = new()
        {
            Id = HrUserId,
            Email = "hr@recruitpro.test",
            FullName = "HR User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass@123"),
            Phone = "0900000002",
            CreatedAt = now,
            UpdatedAt = now
        };

        User manager = new()
        {
            Id = ManagerUserId,
            Email = "manager@recruitpro.test",
            FullName = "Manager User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass@123"),
            Phone = "0900000003",
            CreatedAt = now,
            UpdatedAt = now
        };

        Department engineering = new() { Id = DepartmentId, Name = "Engineering", Description = "Core delivery" };
        Skill dotNet = new() { Id = DotNetSkillId, Name = ".NET" };
        Skill sql = new() { Id = SqlSkillId, Name = "SQL" };

        CandidateProfile profile = new()
        {
            Id = CandidateProfileId,
            UserId = CandidateUserId,
            CurrentPosition = "Backend Engineer",
            ExperienceYears = 4,
            Education = "Bachelor of Computer Science",
            Address = "Ho Chi Minh City",
            Bio = "Builds APIs",
            ResumeUrl = "resumes/candidate/current.pdf",
            ResumeParseStatus = "Completed",
            CandidateEmbeddingStatus = "Completed",
            User = candidate
        };

        profile.CandidateSkills.Add(new CandidateSkill { CandidateId = CandidateProfileId, SkillId = DotNetSkillId, Skill = dotNet, YearsOfExperience = 4 });
        profile.CandidateSkills.Add(new CandidateSkill { CandidateId = CandidateProfileId, SkillId = SqlSkillId, Skill = sql, YearsOfExperience = 3 });
        profile.Resumes.Add(new CandidateResume
        {
            Id = ResumeId,
            CandidateProfileId = CandidateProfileId,
            FileName = "candidate.pdf",
            StorageKey = "resumes/candidate/current.pdf",
            UploadDate = now,
            Version = 1,
            IsCurrent = true
        });
        profile.Projects.Add(new CandidateProject
        {
            Id = Guid.Parse("81000000-0000-0000-0000-000000000001"),
            CandidateProfileId = CandidateProfileId,
            Name = "Recruit API",
            Role = "Backend",
            Description = "API delivery",
            StartMonth = 1,
            StartYear = 2024,
            IsCurrent = true
        });

        Job approvedJob = new()
        {
            Id = ApprovedJobId,
            DepartmentId = DepartmentId,
            CreatedBy = HrUserId,
            Title = "Senior .NET Engineer",
            ShortPitch = "Own backend services",
            Description = "[\"Build APIs\",\"Work with PostgreSQL\"]",
            Requirements = "[\"4+ years .NET\"]",
            Benefits = "[\"Remote days\"]",
            Location = "HCMC",
            WorkMode = WorkMode.Hybrid,
            EmploymentType = EmploymentType.FullTime,
            MinExperienceYears = 3,
            VacancyCount = 2,
            SalaryMin = 2000,
            SalaryMax = 3500,
            Deadline = Timestamp(now.AddDays(30)),
            Status = JobStatus.Approved,
            CreatedAt = Timestamp(now.AddDays(-10)),
            CreatedByNavigation = hr,
            Department = engineering
        };

        approvedJob.JobSkills.Add(new JobSkill { JobId = ApprovedJobId, SkillId = DotNetSkillId, Skill = dotNet, IsRequired = true, MinYearsExperience = 3 });
        approvedJob.JobSkills.Add(new JobSkill { JobId = ApprovedJobId, SkillId = SqlSkillId, Skill = sql, IsRequired = false, MinYearsExperience = 1 });

        Job pendingJob = new()
        {
            Id = PendingJobId,
            DepartmentId = DepartmentId,
            CreatedBy = HrUserId,
            Title = "Platform Engineer",
            ShortPitch = "Manager approval required",
            Description = "[\"Operate services\"]",
            Requirements = "[\"Kubernetes\"]",
            Location = "Da Nang",
            WorkMode = WorkMode.Remote,
            EmploymentType = EmploymentType.Contract,
            MinExperienceYears = 2,
            VacancyCount = 1,
            Status = JobStatus.PendingApproval,
            CreatedAt = Timestamp(now.AddDays(-4)),
            CreatedByNavigation = hr,
            Department = engineering
        };

        Domain.Entities.Application application = new()
        {
            Id = ApplicationId,
            UserId = CandidateUserId,
            JobId = ApprovedJobId,
            ReviewedBy = ManagerUserId,
            Status = ApplicationStatus.ManagerReview,
            AppliedAt = Timestamp(now.AddDays(-3)),
            CoverLetter = "I am interested.",
            RuleScore = 88,
            FinalScore = 88,
            ScoreStatus = "Completed",
            ScoredAt = Timestamp(now.AddDays(-3)),
            User = candidate,
            Job = approvedJob,
            ReviewedByNavigation = manager
        };

        application.Interviews.Add(new Interview
        {
            Id = InterviewId,
            ApplicationId = ApplicationId,
            InterviewDate = Timestamp(now.AddDays(2)),
            MeetingType = MeetingType.Online,
            Status = InterviewStatus.Scheduled,
            Notes = "Initial round"
        });

        db.Roles.AddRange(candidateRole, hrRole, managerRole);
        db.Users.AddRange(candidate, hr, manager);
        db.UserRoles.AddRange(
            new UserRole { UserId = CandidateUserId, RoleId = CandidateRoleId, AssignedAt = now },
            new UserRole { UserId = HrUserId, RoleId = HrRoleId, AssignedAt = now },
            new UserRole { UserId = ManagerUserId, RoleId = ManagerRoleId, AssignedAt = now });
        db.Departments.Add(engineering);
        db.Skills.AddRange(dotNet, sql);
        db.CandidateProfiles.Add(profile);
        db.Jobs.AddRange(approvedJob, pendingJob);
        db.Applications.Add(application);
        await db.SaveChangesAsync();
    }

    private static DateTime TimestampNow() => Timestamp(DateTime.UtcNow);

    private static DateTime Timestamp(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
}

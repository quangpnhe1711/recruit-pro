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
    public static readonly Guid HeadDepartmentRoleId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    public static readonly Guid SystemAdminRoleId = Guid.Parse("10000000-0000-0000-0000-000000000005");
    public static readonly Guid CandidateUserId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid HrUserId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    // A second, real HR-role user that owns NO job/application/candidate — used to assert ownership
    // scoping (valid role, not the owner). It must be a real row because authentication now rejects
    // tokens whose subject does not exist (JwtExtension.OnTokenValidated).
    public static readonly Guid SecondHrUserId = Guid.Parse("20000000-0000-0000-0000-000000000006");
    public static readonly Guid ManagerUserId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly Guid HeadDepartmentUserId = Guid.Parse("20000000-0000-0000-0000-000000000004");
    public static readonly Guid SystemAdminUserId = Guid.Parse("20000000-0000-0000-0000-000000000005");
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
        Role headDepartmentRole = new() { Id = HeadDepartmentRoleId, Name = "HeadDepartment" };
        Role systemAdminRole = new() { Id = SystemAdminRoleId, Name = "SystemAdmin" };

        User candidate = new()
        {
            Id = CandidateUserId,
            Username = "candidate.user",
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
            Username = "hr.user",
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
            Username = "manager.user",
            Email = "manager@recruitpro.test",
            FullName = "Manager User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass@123"),
            Phone = "0900000003",
            CreatedAt = now,
            UpdatedAt = now
        };

        // Second HR user with no ownership over any seeded job/application — the "out-of-scope HR" in
        // ownership tests. A real row so its access token passes authentication and reaches the scoping logic.
        User secondHr = new()
        {
            Id = SecondHrUserId,
            Username = "hr.two",
            Email = "hr2@recruitpro.test",
            FullName = "HR User Two",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass@123"),
            Phone = "0900000006",
            CreatedAt = now,
            UpdatedAt = now
        };

        // Phase 2/3: a dedicated HeadDepartment-role user (distinct from the Manager-role user). Used by
        // the assignable-owners directory and department-head assignment tests.
        User headDepartment = new()
        {
            Id = HeadDepartmentUserId,
            Username = "head.user",
            Email = "head@recruitpro.test",
            FullName = "Head Department User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass@123"),
            Phone = "0900000004",
            CreatedAt = now,
            UpdatedAt = now
        };

        // Phase 2/3: a SystemAdmin user — used to verify the SystemAdmin approval override (a real row so
        // Job.ApprovedBy = SystemAdmin satisfies the FK).
        User systemAdmin = new()
        {
            Id = SystemAdminUserId,
            Username = "admin.user",
            Email = "admin@recruitpro.test",
            FullName = "System Admin User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass@123"),
            Phone = "0900000005",
            CreatedAt = now,
            UpdatedAt = now
        };

        // Phase 1 ownership: the manager user is the Engineering department head in the test seed.
        Department engineering = new() { Id = DepartmentId, Name = "Engineering", Description = "Core delivery", HeadUserId = ManagerUserId };
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
            RecruiterId = HrUserId,
            ApprovedBy = ManagerUserId,
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
            AssignedRecruiterId = HrUserId,
            AssignedDepartmentHeadId = ManagerUserId,
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

        db.Roles.AddRange(candidateRole, hrRole, managerRole, headDepartmentRole, systemAdminRole);
        db.Users.AddRange(candidate, hr, secondHr, manager, headDepartment, systemAdmin);
        db.UserRoles.AddRange(
            new UserRole { UserId = CandidateUserId, RoleId = CandidateRoleId, AssignedAt = now },
            new UserRole { UserId = HrUserId, RoleId = HrRoleId, AssignedAt = now },
            new UserRole { UserId = SecondHrUserId, RoleId = HrRoleId, AssignedAt = now },
            new UserRole { UserId = ManagerUserId, RoleId = ManagerRoleId, AssignedAt = now },
            new UserRole { UserId = HeadDepartmentUserId, RoleId = HeadDepartmentRoleId, AssignedAt = now },
            new UserRole { UserId = SystemAdminUserId, RoleId = SystemAdminRoleId, AssignedAt = now });
        SeedPermissions(db);
        db.Departments.Add(engineering);
        db.Skills.AddRange(dotNet, sql);
        db.CandidateProfiles.Add(profile);
        db.Jobs.AddRange(approvedJob, pendingJob);
        db.Applications.Add(application);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds the RBAC permission catalog and per-role grants, mirroring init.sql: SystemAdmin holds
    /// everything (incl. PERMISSION_MANAGE); HR/Manager/Candidate hold business subsets only. The
    /// [RequirePermission] guards on the SysAdmin console read these rows.
    /// </summary>
    private static void SeedPermissions(AppDbContext db)
    {
        Dictionary<string, Permission> byCode = RecruitPro.Application.Common.RbacCatalog.AllCodes
            .ToDictionary(
                code => code,
                code => new Permission { Id = Guid.NewGuid(), Name = code.Replace('_', ' '), Code = code },
                StringComparer.OrdinalIgnoreCase);
        db.Permissions.AddRange(byCode.Values);

        void Grant(Guid roleId, params string[] codes)
        {
            foreach (string code in codes)
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = byCode[code].Id,
                    AssignedAt = DateTime.UtcNow,
                });
            }
        }

        Grant(SystemAdminRoleId, byCode.Keys.ToArray());
        Grant(HrRoleId,
            "Job_VIEW", "Job_CREATE", "Job_UPDATE",
            "Application_VIEW", "Application_REVIEW",
            "Interview_VIEW", "Interview_CREATE", "Interview_UPDATE",
            "CANDIDATE_PROFILE_VIEW", "DEPARTMENT_VIEW", "SKILL_VIEW", "NOTIFICATION_VIEW");
        Grant(ManagerRoleId,
            "Job_VIEW", "Job_APPROVE",
            "Application_VIEW", "Application_REVIEW",
            "Interview_VIEW", "DEPARTMENT_VIEW", "NOTIFICATION_VIEW", "System_LOG_VIEW");
        Grant(HeadDepartmentRoleId,
            "Interview_VIEW", "DEPARTMENT_VIEW", "SKILL_VIEW", "NOTIFICATION_VIEW");
        Grant(CandidateRoleId,
            "Job_VIEW", "Application_VIEW", "Application_APPLY", "Interview_VIEW",
            "CANDIDATE_PROFILE_VIEW", "CANDIDATE_PROFILE_UPDATE", "SKILL_VIEW", "NOTIFICATION_VIEW");
    }

    private static DateTime TimestampNow() => Timestamp(DateTime.UtcNow);

    private static DateTime Timestamp(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
}

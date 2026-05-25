using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using RecruitPro.Domain.Entities;
using JobApplication = RecruitPro.Domain.Entities.Application;

namespace RecruitPro.Infrastructure.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<JobApplication> Applications { get; set; }

    public virtual DbSet<CandidateProfile> CandidateProfiles { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Interview> Interviews { get; set; }

    public virtual DbSet<Job> Jobs { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Skill> Skills { get; set; }

    public virtual DbSet<SystemLog> SystemLogs { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=recruit_pro;Username=postgres;Password=123456");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("application_status", new[] { "Pending", "Reviewing", "Interviewing", "ManagerReview", "Accepted", "Rejected" })
            .HasPostgresEnum("employment_type", new[] { "FullTime", "PartTime", "Remote", "Hybrid", "Internship", "Contract" })
            .HasPostgresEnum("interview_status", new[] { "Scheduled", "Completed", "Cancelled" })
            .HasPostgresEnum("job_status", new[] { "Draft", "PendingApproval", "Approved", "Closed", "Rejected" })
            .HasPostgresEnum("meeting_type", new[] { "Online", "Offline" })
            .HasPostgresEnum("notification_type", new[] { "System", "Job", "Interview", "Application" })
            .HasPostgresEnum("user_status", new[] { "Active", "Inactive", "Blocked" })
            .HasPostgresExtension("pgcrypto");

        modelBuilder.Entity<JobApplication>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("applications_pkey");

            entity.ToTable("applications");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AppliedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("applied_at");
            entity.Property(e => e.CandidateId).HasColumnName("candidate_id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");

            entity.HasOne(d => d.Candidate).WithMany(p => p.Applications)
                .HasForeignKey(d => d.CandidateId)
                .HasConstraintName("applications_candidate_id_fkey");

            entity.HasOne(d => d.Job).WithMany(p => p.Applications)
                .HasForeignKey(d => d.JobId)
                .HasConstraintName("applications_job_id_fkey");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.Applications)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("applications_reviewed_by_fkey");
        });

        modelBuilder.Entity<CandidateProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("candidate_profiles_pkey");

            entity.ToTable("candidate_profiles");

            entity.HasIndex(e => e.UserId, "candidate_profiles_user_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.Bio).HasColumnName("bio");
            entity.Property(e => e.CurrentPosition)
                .HasMaxLength(255)
                .HasColumnName("current_position");
            entity.Property(e => e.Education).HasColumnName("education");
            entity.Property(e => e.ExperienceYears)
                .HasDefaultValue(0)
                .HasColumnName("experience_years");
            entity.Property(e => e.GithubUrl).HasColumnName("github_url");
            entity.Property(e => e.LinkedinUrl).HasColumnName("linkedin_url");
            entity.Property(e => e.ResumeUrl).HasColumnName("resume_url");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithOne(p => p.CandidateProfile)
                .HasForeignKey<CandidateProfile>(d => d.UserId)
                .HasConstraintName("candidate_profiles_user_id_fkey");

            entity.HasMany(d => d.Skills).WithMany(p => p.Candidates)
                .UsingEntity<Dictionary<string, object>>(
                    "CandidateSkill",
                    r => r.HasOne<Skill>().WithMany()
                        .HasForeignKey("SkillId")
                        .HasConstraintName("candidate_skills_skill_id_fkey"),
                    l => l.HasOne<CandidateProfile>().WithMany()
                        .HasForeignKey("CandidateId")
                        .HasConstraintName("candidate_skills_candidate_id_fkey"),
                    j =>
                    {
                        j.HasKey("CandidateId", "SkillId").HasName("candidate_skills_pkey");
                        j.ToTable("candidate_skills");
                        j.IndexerProperty<Guid>("CandidateId").HasColumnName("candidate_id");
                        j.IndexerProperty<Guid>("SkillId").HasColumnName("skill_id");
                    });
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("departments_pkey");

            entity.ToTable("departments");

            entity.HasIndex(e => e.Name, "departments_name_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Interview>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("interviews_pkey");

            entity.ToTable("interviews");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ApplicationId).HasColumnName("application_id");
            entity.Property(e => e.InterviewDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("interview_date");
            entity.Property(e => e.Location).HasColumnName("location");
            entity.Property(e => e.MeetingLink).HasColumnName("meeting_link");
            entity.Property(e => e.Notes).HasColumnName("notes");

            entity.HasOne(d => d.Application).WithMany(p => p.Interviews)
                .HasForeignKey(d => d.ApplicationId)
                .HasConstraintName("interviews_application_id_fkey");
        });

        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("jobs_pkey");

            entity.ToTable("jobs");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Deadline)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("deadline");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Location)
                .HasMaxLength(255)
                .HasColumnName("location");
            entity.Property(e => e.Requirements).HasColumnName("requirements");
            entity.Property(e => e.SalaryMax)
                .HasPrecision(15, 2)
                .HasColumnName("salary_max");
            entity.Property(e => e.SalaryMin)
                .HasPrecision(15, 2)
                .HasColumnName("salary_min");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.JobApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("jobs_approved_by_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.JobCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("jobs_created_by_fkey");

            entity.HasOne(d => d.Department).WithMany(p => p.Jobs)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("jobs_department_id_fkey");

            entity.HasMany(d => d.Skills).WithMany(p => p.Jobs)
                .UsingEntity<Dictionary<string, object>>(
                    "JobSkill",
                    r => r.HasOne<Skill>().WithMany()
                        .HasForeignKey("SkillId")
                        .HasConstraintName("job_skills_skill_id_fkey"),
                    l => l.HasOne<Job>().WithMany()
                        .HasForeignKey("JobId")
                        .HasConstraintName("job_skills_job_id_fkey"),
                    j =>
                    {
                        j.HasKey("JobId", "SkillId").HasName("job_skills_pkey");
                        j.ToTable("job_skills");
                        j.IndexerProperty<Guid>("JobId").HasColumnName("job_id");
                        j.IndexerProperty<Guid>("SkillId").HasColumnName("skill_id");
                    });
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notifications_pkey");

            entity.ToTable("notifications");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.IsRead)
                .HasDefaultValue(false)
                .HasColumnName("is_read");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("notifications_user_id_fkey");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("permissions_pkey");

            entity.ToTable("permissions");

            entity.HasIndex(e => e.Code, "permissions_code_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(100)
                .HasColumnName("code");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("refresh_tokens_pkey");

            entity.ToTable("refresh_tokens");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ExpiryDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("expiry_date");
            entity.Property(e => e.Token).HasColumnName("token");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("refresh_tokens_user_id_fkey");
        });


        modelBuilder.Entity<Skill>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("skills_pkey");

            entity.ToTable("skills");

            entity.HasIndex(e => e.Name, "skills_name_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<SystemLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("system_logs_pkey");

            entity.ToTable("system_logs");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Action)
                .HasMaxLength(255)
                .HasColumnName("action");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.SystemLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("system_logs_user_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AvatarUrl).HasColumnName("avatar_url");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(255)
                .HasColumnName("full_name");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data.Converters;
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

    public virtual DbSet<ApplicationOffer> ApplicationOffers { get; set; }

    public virtual DbSet<ApplicationOfferBenefit> ApplicationOfferBenefits { get; set; }

    public virtual DbSet<CandidateProfile> CandidateProfiles { get; set; }

    public virtual DbSet<CandidateProject> CandidateProjects { get; set; }

    public virtual DbSet<CandidateResume> CandidateResumes { get; set; }

    public virtual DbSet<CandidateSkill> CandidateSkills { get; set; }

    public virtual DbSet<CopilotCandidateTag> CopilotCandidateTags { get; set; }

    public virtual DbSet<CopilotConversation> CopilotConversations { get; set; }

    public virtual DbSet<CopilotMessage> CopilotMessages { get; set; }

    public virtual DbSet<CopilotRankingResult> CopilotRankingResults { get; set; }

    public virtual DbSet<CopilotRankingSession> CopilotRankingSessions { get; set; }

    public virtual DbSet<CopilotSavedRule> CopilotSavedRules { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Interview> Interviews { get; set; }

    public virtual DbSet<Job> Jobs { get; set; }

    public virtual DbSet<JobSkill> JobSkills { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<OfferBenefit> OfferBenefits { get; set; }

    public virtual DbSet<OfferCurrency> OfferCurrencies { get; set; }

    public virtual DbSet<OfferTemplate> OfferTemplates { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<RolePermission> RolePermissions { get; set; }

    public virtual DbSet<Skill> Skills { get; set; }

    public virtual DbSet<SystemLog> SystemLogs { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto");

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
            entity.Property(e => e.CoverLetter)
                .HasColumnType("text")
                .HasColumnName("cover_letter");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.FinalScore)
                .HasPrecision(5, 2)
                .HasColumnName("final_score");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.RuleScore)
                .HasPrecision(5, 2)
                .HasColumnName("rule_score");
            entity.Property(e => e.ScoreError).HasColumnName("score_error");
            entity.Property(e => e.ScoreStatus)
                .HasMaxLength(50)
                .HasColumnName("score_status");
            entity.Property(e => e.ScoredAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("scored_at");
            entity.Property(e => e.SemanticScore)
                .HasPrecision(5, 2)
                .HasColumnName("semantic_score");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Status)
                .HasConversion(new ApplicationStatusValueConverter())
                .HasMaxLength(50)
                .HasColumnType("character varying(50)")
                .HasColumnName("status");

            entity.HasOne(d => d.Job).WithMany(p => p.Applications)
                .HasForeignKey(d => d.JobId)
                .HasConstraintName("applications_job_id_fkey");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.ApplicationReviewedByNavigations)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("applications_reviewed_by_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Applications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("applications_user_id_fkey");
        });

        modelBuilder.Entity<ApplicationOffer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("application_offers_pkey");

            entity.ToTable("application_offers");

            entity.HasIndex(e => e.ApplicationId, "application_offers_application_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ApplicationId).HasColumnName("application_id");
            entity.Property(e => e.BaseSalary)
                .HasPrecision(15, 2)
                .HasColumnName("base_salary");
            entity.Property(e => e.BonusDescription).HasColumnName("bonus_description");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CurrencyCode)
                .HasMaxLength(10)
                .HasColumnName("currency_code");
            entity.Property(e => e.EmploymentType)
                .HasMaxLength(100)
                .HasColumnName("employment_type");
            entity.Property(e => e.EquityNotes).HasColumnName("equity_notes");
            entity.Property(e => e.OfferTemplateId).HasColumnName("offer_template_id");
            entity.Property(e => e.PersonalMessage).HasColumnName("personal_message");
            entity.Property(e => e.ProbationPeriod)
                .HasMaxLength(100)
                .HasColumnName("probation_period");
            entity.Property(e => e.ProposedStartDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("proposed_start_date");
            entity.Property(e => e.ReportingManagerId).HasColumnName("reporting_manager_id");
            entity.Property(e => e.SentAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("sent_at");
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Application).WithOne(p => p.Offer)
                .HasForeignKey<ApplicationOffer>(d => d.ApplicationId)
                .HasConstraintName("application_offers_application_id_fkey");

            entity.HasOne(d => d.Currency).WithMany(p => p.ApplicationOffers)
                .HasForeignKey(d => d.CurrencyCode)
                .HasPrincipalKey(p => p.Code)
                .HasConstraintName("application_offers_currency_code_fkey");

            entity.HasOne(d => d.OfferTemplate).WithMany(p => p.ApplicationOffers)
                .HasForeignKey(d => d.OfferTemplateId)
                .HasConstraintName("application_offers_offer_template_id_fkey");

            entity.HasOne(d => d.ReportingManagerNavigation).WithMany(p => p.ReportingManagerOffers)
                .HasForeignKey(d => d.ReportingManagerId)
                .HasConstraintName("application_offers_reporting_manager_id_fkey");
        });

        modelBuilder.Entity<ApplicationOfferBenefit>(entity =>
        {
            entity.HasKey(e => new { e.OfferId, e.BenefitId }).HasName("application_offer_benefits_pkey");

            entity.ToTable("application_offer_benefits");

            entity.Property(e => e.OfferId).HasColumnName("offer_id");
            entity.Property(e => e.BenefitId).HasColumnName("benefit_id");

            entity.HasOne(d => d.Benefit).WithMany(p => p.ApplicationOfferBenefits)
                .HasForeignKey(d => d.BenefitId)
                .HasConstraintName("application_offer_benefits_benefit_id_fkey");

            entity.HasOne(d => d.Offer).WithMany(p => p.ApplicationOfferBenefits)
                .HasForeignKey(d => d.OfferId)
                .HasConstraintName("application_offer_benefits_offer_id_fkey");
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
            entity.Property(e => e.CertificationRecordsJson).HasColumnName("certification_records_json");
            entity.Property(e => e.CurrentPosition)
                .HasMaxLength(255)
                .HasColumnName("current_position");
            entity.Property(e => e.Education).HasColumnName("education");
            entity.Property(e => e.EducationRecordsJson).HasColumnName("education_records_json");
            entity.Property(e => e.ExperienceEntriesJson).HasColumnName("experience_entries_json");
            entity.Property(e => e.ExperienceYears)
                .HasDefaultValue(0)
                .HasColumnName("experience_years");
            entity.Property(e => e.CandidateEmbeddingError).HasColumnName("candidate_embedding_error");
            entity.Property(e => e.CandidateEmbeddingStatus)
                .HasMaxLength(50)
                .HasColumnName("candidate_embedding_status");
            entity.Property(e => e.CandidateEmbeddingVectorJson)
                .HasColumnType("jsonb")
                .HasColumnName("candidate_embedding_vector");
            entity.Property(e => e.CandidateEmbeddingTextHash)
                .HasMaxLength(128)
                .HasColumnName("candidate_embedding_text_hash");
            entity.Property(e => e.CandidateEmbeddingUpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("candidate_embedding_updated_at");
            entity.Property(e => e.GithubUrl).HasColumnName("github_url");
            entity.Property(e => e.LanguageRecordsJson).HasColumnName("language_records_json");
            entity.Property(e => e.LinkedinUrl).HasColumnName("linkedin_url");
            entity.Property(e => e.ParsedResumeJson)
                .HasColumnType("jsonb")
                .HasColumnName("parsed_resume_json");
            entity.Property(e => e.ResumeUrl).HasColumnName("resume_url");
            entity.Property(e => e.ResumeExtractedText).HasColumnName("resume_extracted_text");
            entity.Property(e => e.ResumeParseError).HasColumnName("resume_parse_error");
            entity.Property(e => e.ResumeParseModel)
                .HasMaxLength(100)
                .HasColumnName("resume_parse_model");
            entity.Property(e => e.ResumeParsedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("resume_parsed_at");
            entity.Property(e => e.ResumeParseStatus)
                .HasMaxLength(50)
                .HasColumnName("resume_parse_status");
            entity.Property(e => e.ResumeParserWarningsJson)
                .HasColumnType("jsonb")
                .HasColumnName("resume_parser_warnings_json");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithOne(p => p.CandidateProfile)
                .HasForeignKey<CandidateProfile>(d => d.UserId)
                .HasConstraintName("candidate_profiles_user_id_fkey");

            entity.HasMany(d => d.Projects).WithOne(p => p.CandidateProfile)
                .HasForeignKey(p => p.CandidateProfileId)
                .HasConstraintName("candidate_projects_candidate_profile_id_fkey");

            entity.HasMany(d => d.Resumes).WithOne(p => p.CandidateProfile)
                .HasForeignKey(p => p.CandidateProfileId)
                .HasConstraintName("candidate_resumes_candidate_profile_id_fkey");

            entity.HasMany(d => d.CandidateSkills).WithOne(p => p.Candidate)
                .HasForeignKey(p => p.CandidateId)
                .HasConstraintName("candidate_skills_candidate_id_fkey");
        });

        modelBuilder.Entity<CandidateProject>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("candidate_projects_pkey");

            entity.ToTable("candidate_projects");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CandidateProfileId).HasColumnName("candidate_profile_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EndMonth).HasColumnName("end_month");
            entity.Property(e => e.EndYear).HasColumnName("end_year");
            entity.Property(e => e.IsCurrent)
                .HasDefaultValue(false)
                .HasColumnName("is_current");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Role)
                .HasMaxLength(255)
                .HasColumnName("role");
            entity.Property(e => e.StartMonth).HasColumnName("start_month");
            entity.Property(e => e.StartYear).HasColumnName("start_year");
            entity.Property(e => e.TechnologiesJson).HasColumnName("technologies_json");
        });

        modelBuilder.Entity<CandidateResume>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("candidate_resumes_pkey");

            entity.ToTable("candidate_resumes");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CandidateProfileId).HasColumnName("candidate_profile_id");
            entity.Property(e => e.FileName)
                .HasMaxLength(255)
                .HasColumnName("file_name");
            entity.Property(e => e.IsCurrent)
                .HasDefaultValue(false)
                .HasColumnName("is_current");
            entity.Property(e => e.StorageKey).HasColumnName("storage_key");
            entity.Property(e => e.UploadDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("upload_date");
            entity.Property(e => e.Version).HasColumnName("version");
        });

        modelBuilder.Entity<CandidateSkill>(entity =>
        {
            entity.HasKey(e => new { e.CandidateId, e.SkillId }).HasName("candidate_skills_pkey");

            entity.ToTable("candidate_skills");

            entity.Property(e => e.CandidateId).HasColumnName("candidate_id");
            entity.Property(e => e.SkillId).HasColumnName("skill_id");
            entity.Property(e => e.YearsOfExperience)
                .HasPrecision(5, 1)
                .HasColumnName("years_of_experience");

            entity.HasOne(d => d.Skill).WithMany(p => p.CandidateSkills)
                .HasForeignKey(d => d.SkillId)
                .HasConstraintName("candidate_skills_skill_id_fkey");
        });

        modelBuilder.Entity<CopilotConversation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("copilot_conversations_pkey");
            entity.ToTable("copilot_conversations");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Title).HasMaxLength(200).HasColumnName("title");
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("Active").HasColumnName("status");
            entity.Property(e => e.LatestRankingSessionId).HasColumnName("latest_ranking_session_id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasIndex(e => new { e.JobId, e.UserId, e.CreatedAt }, "ix_copilot_conversations_job_user_created_at");
            entity.HasIndex(e => e.LatestRankingSessionId, "ix_copilot_conversations_latest_ranking_session_id");
            entity.HasIndex(e => new { e.JobId, e.UserId }, "ux_copilot_conversations_active_job_user")
                .IsUnique()
                .HasFilter("status = 'Active'");

            entity.HasOne(e => e.Job).WithMany().HasForeignKey(e => e.JobId).HasConstraintName("copilot_conversations_job_id_fkey");
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).HasConstraintName("copilot_conversations_user_id_fkey");
            entity.HasOne(e => e.LatestRankingSession).WithMany().HasForeignKey(e => e.LatestRankingSessionId).HasConstraintName("copilot_conversations_latest_ranking_session_id_fkey");
        });

        modelBuilder.Entity<CopilotMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("copilot_messages_pkey");
            entity.ToTable("copilot_messages");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.Role).HasMaxLength(20).HasColumnName("role");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.MetadataJson).HasColumnType("jsonb").HasColumnName("metadata_json");
            entity.Property(e => e.SequenceNo).HasColumnName("sequence_no");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasIndex(e => new { e.ConversationId, e.SequenceNo }, "ix_copilot_messages_conversation_sequence");
            entity.HasOne(e => e.Conversation).WithMany(e => e.Messages).HasForeignKey(e => e.ConversationId).HasConstraintName("copilot_messages_conversation_id_fkey");
        });

        modelBuilder.Entity<CopilotRankingSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("copilot_ranking_sessions_pkey");
            entity.ToTable("copilot_ranking_sessions");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.UserPrompt).HasColumnName("user_prompt");
            entity.Property(e => e.NormalizedRulesJson).HasColumnType("jsonb").HasColumnName("normalized_rules_json");
            entity.Property(e => e.TotalCandidates).HasColumnName("total_candidates");
            entity.Property(e => e.ModelName).HasMaxLength(100).HasColumnName("model_name");
            entity.Property(e => e.PromptTokens).HasColumnName("prompt_tokens");
            entity.Property(e => e.CompletionTokens).HasColumnName("completion_tokens");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasIndex(e => new { e.ConversationId, e.CreatedAt }, "ix_copilot_ranking_sessions_conversation_created_at");
            entity.HasIndex(e => new { e.JobId, e.CreatedAt }, "ix_copilot_ranking_sessions_job_created_at");
            entity.HasOne(e => e.Job).WithMany().HasForeignKey(e => e.JobId).HasConstraintName("copilot_ranking_sessions_job_id_fkey");
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).HasConstraintName("copilot_ranking_sessions_user_id_fkey");
            entity.HasOne(e => e.Conversation).WithMany(e => e.RankingSessions).HasForeignKey(e => e.ConversationId).HasConstraintName("copilot_ranking_sessions_conversation_id_fkey");
        });

        modelBuilder.Entity<CopilotRankingResult>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("copilot_ranking_results_pkey");
            entity.ToTable("copilot_ranking_results");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.RankingSessionId).HasColumnName("ranking_session_id");
            entity.Property(e => e.CandidateUserId).HasColumnName("candidate_user_id");
            entity.Property(e => e.ApplicationId).HasColumnName("application_id");
            entity.Property(e => e.RankPosition).HasColumnName("rank_position");
            entity.Property(e => e.TotalScore).HasPrecision(5, 2).HasColumnName("total_score");
            entity.Property(e => e.SkillScore).HasPrecision(5, 2).HasColumnName("skill_score");
            entity.Property(e => e.ExperienceScore).HasPrecision(5, 2).HasColumnName("experience_score");
            entity.Property(e => e.EducationScore).HasPrecision(5, 2).HasColumnName("education_score");
            entity.Property(e => e.ProjectScore).HasPrecision(5, 2).HasColumnName("project_score");
            entity.Property(e => e.Recommendation).HasMaxLength(30).HasColumnName("recommendation");
            entity.Property(e => e.RejectReason).HasColumnName("reject_reason");
            entity.Property(e => e.IsAutoRejected).HasDefaultValue(false).HasColumnName("is_auto_rejected");
            entity.Property(e => e.StrengthsJson).HasColumnType("jsonb").HasColumnName("strengths_json");
            entity.Property(e => e.WeaknessesJson).HasColumnType("jsonb").HasColumnName("weaknesses_json");
            entity.Property(e => e.ExplanationJson).HasColumnType("jsonb").HasColumnName("explanation_json");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasIndex(e => new { e.RankingSessionId, e.RankPosition }, "ix_copilot_ranking_results_session_rank");
            entity.HasIndex(e => new { e.RankingSessionId, e.IsAutoRejected, e.TotalScore }, "ix_copilot_ranking_results_session_reject_score");
            entity.HasOne(e => e.RankingSession).WithMany(e => e.Results).HasForeignKey(e => e.RankingSessionId).HasConstraintName("copilot_ranking_results_ranking_session_id_fkey");
            entity.HasOne(e => e.CandidateUser).WithMany().HasForeignKey(e => e.CandidateUserId).HasConstraintName("copilot_ranking_results_candidate_user_id_fkey");
            entity.HasOne(e => e.Application).WithMany().HasForeignKey(e => e.ApplicationId).HasConstraintName("copilot_ranking_results_application_id_fkey");
        });

        modelBuilder.Entity<CopilotSavedRule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("copilot_saved_rules_pkey");
            entity.ToTable("copilot_saved_rules");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Name).HasMaxLength(200).HasColumnName("name");
            entity.Property(e => e.RuleJson).HasColumnType("jsonb").HasColumnName("rule_json");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false).HasColumnName("is_deleted");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("updated_at");
            entity.Property(e => e.DeletedAt).HasColumnType("timestamp without time zone").HasColumnName("deleted_at");

            entity.HasIndex(e => new { e.JobId, e.IsActive }, "ix_copilot_saved_rules_job_active");
            entity.HasIndex(e => new { e.JobId, e.UserId, e.UpdatedAt }, "ix_copilot_saved_rules_job_user_updated_at");
            entity.HasOne(e => e.Job).WithMany().HasForeignKey(e => e.JobId).HasConstraintName("copilot_saved_rules_job_id_fkey");
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).HasConstraintName("copilot_saved_rules_user_id_fkey");
        });

        modelBuilder.Entity<CopilotCandidateTag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("copilot_candidate_tags_pkey");
            entity.ToTable("copilot_candidate_tags");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.CandidateUserId).HasColumnName("candidate_user_id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.RankingSessionId).HasColumnName("ranking_session_id");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.TagName).HasMaxLength(100).HasColumnName("tag_name");
            entity.Property(e => e.Source).HasMaxLength(20).HasDefaultValue("AI").HasColumnName("source");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasIndex(e => new { e.JobId, e.TagName }, "ix_copilot_candidate_tags_job_tag");
            entity.HasOne(e => e.CandidateUser).WithMany().HasForeignKey(e => e.CandidateUserId).HasConstraintName("copilot_candidate_tags_candidate_user_id_fkey");
            entity.HasOne(e => e.Job).WithMany().HasForeignKey(e => e.JobId).HasConstraintName("copilot_candidate_tags_job_id_fkey");
            entity.HasOne(e => e.RankingSession).WithMany().HasForeignKey(e => e.RankingSessionId).HasConstraintName("copilot_candidate_tags_ranking_session_id_fkey");
            entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).HasConstraintName("copilot_candidate_tags_created_by_user_id_fkey");
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
            entity.Property(e => e.MeetingType)
                .HasConversion<string>()
                .HasMaxLength(50)
                .HasColumnType("character varying(50)")
                .HasColumnName("meeting_type");
            entity.Property(e => e.MeetingLink).HasColumnName("meeting_link");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .HasColumnType("character varying(50)")
                .HasColumnName("status");

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
            entity.Property(e => e.Benefits).HasColumnName("benefits");
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .HasColumnType("character varying(50)")
                .HasColumnName("status");

            entity.Property(e => e.EmploymentType)
                .HasConversion<string>()
                .HasMaxLength(50)
                .HasColumnType("character varying(50)")
                .HasColumnName("employment_type");

            entity.Property(e => e.WorkMode)
                .HasConversion<string>()
                .HasMaxLength(50)
                .HasColumnType("character varying(50)")
                .HasColumnName("work_mode");
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
            entity.Property(e => e.JobEmbeddingError).HasColumnName("job_embedding_error");
            entity.Property(e => e.JobEmbeddingStatus)
                .HasMaxLength(50)
                .HasColumnName("job_embedding_status");
            entity.Property(e => e.JobEmbeddingVectorJson)
                .HasColumnType("jsonb")
                .HasColumnName("job_embedding_vector");
            entity.Property(e => e.JobEmbeddingTextHash)
                .HasMaxLength(128)
                .HasColumnName("job_embedding_text_hash");
            entity.Property(e => e.JobEmbeddingUpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("job_embedding_updated_at");
            entity.Property(e => e.Location)
                .HasMaxLength(255)
                .HasColumnName("location");
            entity.Property(e => e.MinExperienceYears)
                .HasDefaultValue(0)
                .HasColumnName("min_experience_years");
            entity.Property(e => e.Requirements).HasColumnName("requirements");
            entity.Property(e => e.SalaryMax)
                .HasPrecision(15, 2)
                .HasColumnName("salary_max");
            entity.Property(e => e.SalaryMin)
                .HasPrecision(15, 2)
                .HasColumnName("salary_min");
            entity.Property(e => e.ShortPitch)
                .HasMaxLength(500)
                .HasColumnName("short_pitch");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.VacancyCount)
                .HasDefaultValue(1)
                .HasColumnName("vacancy_count");

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
        });

        modelBuilder.Entity<JobSkill>(entity =>
        {
            entity.HasKey(e => new { e.JobId, e.SkillId }).HasName("job_skills_pkey");

            entity.ToTable("job_skills");

            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.SkillId).HasColumnName("skill_id");
            entity.Property(e => e.IsRequired)
                .HasDefaultValue(true)
                .HasColumnName("is_required");
            entity.Property(e => e.MinYearsExperience)
                .HasPrecision(5, 1)
                .HasColumnName("min_years_experience");

            entity.HasOne(d => d.Job).WithMany(p => p.JobSkills)
                .HasForeignKey(d => d.JobId)
                .HasConstraintName("job_skills_job_id_fkey");

            entity.HasOne(d => d.Skill).WithMany(p => p.JobSkills)
                .HasForeignKey(d => d.SkillId)
                .HasConstraintName("job_skills_skill_id_fkey");
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

        modelBuilder.Entity<OfferBenefit>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("offer_benefits_pkey");

            entity.ToTable("offer_benefits");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DisplayOrder).HasColumnName("display_order");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
        });

        modelBuilder.Entity<OfferCurrency>(entity =>
        {
            entity.HasKey(e => e.Code).HasName("offer_currencies_pkey");

            entity.ToTable("offer_currencies");

            entity.Property(e => e.Code)
                .HasMaxLength(10)
                .HasColumnName("code");
            entity.Property(e => e.DisplayOrder).HasColumnName("display_order");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Symbol)
                .HasMaxLength(10)
                .HasColumnName("symbol");
        });

        modelBuilder.Entity<OfferTemplate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("offer_templates_pkey");

            entity.ToTable("offer_templates");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DisplayOrder).HasColumnName("display_order");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.TemplateBody).HasColumnName("template_body");
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

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pkey");

            entity.ToTable("roles");

            entity.HasIndex(e => e.Name, "roles_name_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => new { e.RoleId, e.PermissionId }).HasName("role_permissions_pkey");

            entity.ToTable("role_permissions");

            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.PermissionId).HasColumnName("permission_id");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("assigned_at");

            entity.HasOne(d => d.Permission).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.PermissionId)
                .HasConstraintName("role_permissions_permission_id_fkey");

            entity.HasOne(d => d.Role).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("role_permissions_role_id_fkey");
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

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId }).HasName("user_roles_pkey");

            entity.ToTable("user_roles");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("assigned_at");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("user_roles_role_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("user_roles_user_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

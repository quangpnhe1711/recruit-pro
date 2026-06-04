using System;
using System.Collections.Generic;

namespace RecruitPro.Domain.Entities;

public partial class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? AvatarUrl { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Application> Applications { get; set; } = new List<Application>();

    public virtual ICollection<Application> ApplicationReviewedByNavigations { get; set; } = new List<Application>();

    public virtual CandidateProfile? CandidateProfile { get; set; }

    public virtual ICollection<Job> JobApprovedByNavigations { get; set; } = new List<Job>();

    public virtual ICollection<Job> JobCreatedByNavigations { get; set; } = new List<Job>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public virtual ICollection<SystemLog> SystemLogs { get; set; } = new List<SystemLog>();

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

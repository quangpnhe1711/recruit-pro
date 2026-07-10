using System;
using System.Collections.Generic;

namespace RecruitPro.Domain.Entities;

public partial class User
{
    public Guid Id { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Account status string backed by <see cref="RecruitPro.Domain.Enums.UserStatus"/> values
    /// (Active/Inactive/Blocked). The DB column has existed since the initial schema (users.status,
    /// default 'Active'); non-Active accounts cannot log in and lose their RBAC grants.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Monotonic token generation. Every issued access token embeds the value current at login;
    /// deactivating an account bumps this so all previously-issued access tokens fail validation
    /// (see JwtExtension.OnTokenValidated) — the deactivation takes effect immediately, not only
    /// after the short access-token lifetime elapses.
    /// </summary>
    public int TokenVersion { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Application> Applications { get; set; } = new List<Application>();

    public virtual ICollection<Application> ApplicationReviewedByNavigations { get; set; } = new List<Application>();

    public virtual ICollection<ApplicationOffer> ReportingManagerOffers { get; set; } = new List<ApplicationOffer>();

    public virtual CandidateProfile? CandidateProfile { get; set; }

    public virtual ICollection<Job> JobApprovedByNavigations { get; set; } = new List<Job>();

    public virtual ICollection<Job> JobCreatedByNavigations { get; set; } = new List<Job>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public virtual ICollection<SystemLog> SystemLogs { get; set; } = new List<SystemLog>();

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

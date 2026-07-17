using System;
using System.Collections.Generic;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Domain.Entities;

public partial class Interview
{
    public Guid Id { get; set; }

    public Guid ApplicationId { get; set; }

    public DateTime InterviewDate { get; set; }

    // The internal user (HR/Manager) running this interview. Nullable: legacy rows have none.
    public Guid? InterviewerId { get; set; }

    // Planned length in minutes; drives EndAt/time-label display and double-booking conflict math.
    // DB default 60 backfills legacy rows.
    public int DurationMinutes { get; set; }

    public MeetingType? MeetingType { get; set; }

    public string? MeetingLink { get; set; }

    public string? Location { get; set; }

    public string? Notes { get; set; }

    public InterviewStatus? Status { get; set; }

    // Candidate attendance confirmation (interview:confirm-own). Null = not confirmed yet.
    public DateTime? CandidateConfirmedAt { get; set; }

    public virtual Application Application { get; set; } = null!;

    public virtual User? Interviewer { get; set; }

    // Post-interview scorecard (one per interview, internal to HR/Manager).
    public virtual InterviewEvaluation? Evaluation { get; set; }
}

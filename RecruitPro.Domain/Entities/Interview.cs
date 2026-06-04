using System;
using System.Collections.Generic;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Domain.Entities;

public partial class Interview
{
    public Guid Id { get; set; }

    public Guid ApplicationId { get; set; }

    public DateTime InterviewDate { get; set; }

    public MeetingType? MeetingType { get; set; }

    public string? MeetingLink { get; set; }

    public string? Location { get; set; }

    public string? Notes { get; set; }

    public InterviewStatus? Status { get; set; }

    public virtual Application Application { get; set; } = null!;
}

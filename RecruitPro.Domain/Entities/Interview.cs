using System;
using System.Collections.Generic;

namespace RecruitPro.Domain.Entities;

public partial class Interview
{
    public Guid Id { get; set; }

    public Guid ApplicationId { get; set; }

    public DateTime InterviewDate { get; set; }

    public string? MeetingLink { get; set; }

    public string? Location { get; set; }

    public string? Notes { get; set; }

    public virtual Application Application { get; set; } = null!;
}

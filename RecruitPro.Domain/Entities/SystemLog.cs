using System;
using System.Collections.Generic;

namespace RecruitPro.Domain.Entities;

public partial class SystemLog
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string? Action { get; set; }

    public string? Description { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? User { get; set; }
}

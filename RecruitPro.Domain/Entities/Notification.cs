using System;
using System.Collections.Generic;

namespace RecruitPro.Domain.Entities;

public partial class Notification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string? EventCode { get; set; }

    public string? Title { get; set; }

    public string? Body { get; set; }

    public string? Content { get; set; }

    public string? DataJson { get; set; }

    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }

    public string? Type { get; set; }

    public bool? IsRead { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}

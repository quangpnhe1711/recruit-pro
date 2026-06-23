namespace RecruitPro.Application.DTOs.Response;

public class NotificationDto
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string EventCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public object? Data { get; set; }

    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}

namespace RecruitPro.Application.DTOs.Response;

public class NotificationListResponseDto
{
    public List<NotificationDto> Items { get; set; } = [];

    public ApiEnvelopeMeta? Meta { get; set; }
}

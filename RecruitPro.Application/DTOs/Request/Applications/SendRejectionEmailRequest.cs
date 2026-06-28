namespace RecruitPro.Application.DTOs.Request.Applications;

/// <summary>
/// Body for the rejection email flow (POST /api/hr/applications/{id}/rejection-email). The application
/// only transitions to <c>Rejected</c> after the email is sent — Subject and Body are required so a real
/// message is composed, never an empty one.
/// </summary>
public class SendRejectionEmailRequest
{
    public string? Subject { get; set; }
    public string? Body { get; set; }
}

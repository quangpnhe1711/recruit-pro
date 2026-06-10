namespace RecruitPro.Application.Interfaces;

public interface IEmailService
{
    Task SendCandidateInvitationAsync(string email, string fullName, string temporaryPassword, string loginUrl);
    Task SendPasswordResetAsync(string email, string fullName, string temporaryPassword, string loginUrl);
}

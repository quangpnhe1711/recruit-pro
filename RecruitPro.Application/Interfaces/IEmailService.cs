namespace RecruitPro.Application.Interfaces;

public interface IEmailService
{
    Task SendCandidateInvitationAsync(string email, string fullName, string temporaryPassword, string loginUrl);
    Task SendPasswordResetAsync(string email, string fullName, string temporaryPassword, string loginUrl);

    // Offer/Reject decisions are email-gated: the status transition happens only after the email send
    // succeeds (see OfferService.SendOfferAsync / ApplicationService.SendRejectionEmailAsync). A thrown
    // exception means the send failed and the application status must NOT change.
    Task SendOfferEmailAsync(string email, string fullName, string jobTitle, string subject, string body);
    Task SendRejectionEmailAsync(string email, string fullName, string jobTitle, string subject, string body);
}

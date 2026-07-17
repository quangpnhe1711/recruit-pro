namespace RecruitPro.Application.Interfaces;

public interface IEmailService
{
    Task SendCandidateInvitationAsync(string email, string fullName, string temporaryPassword, string loginUrl);

    // Sends a single-use reset LINK (not a password). The old password stays valid until the user
    // completes the reset, so a slow/spam-filed email never locks anyone out.
    Task SendPasswordResetAsync(string email, string fullName, string resetUrl);
    Task SendApplicationEmailAsync(string email, string fullName, string jobTitle, string subject, string body);

    // Offer/Reject decisions are email-gated: the status transition happens only after the email send
    // succeeds (see OfferService.SendOfferAsync / ApplicationService.SendRejectionEmailAsync). A thrown
    // exception means the send failed and the application status must NOT change.
    Task SendOfferEmailAsync(string email, string fullName, string jobTitle, string subject, string body);
    Task SendRejectionEmailAsync(
        string email,
        string fullName,
        string jobTitle,
        string subject,
        string body,
        string? replyToEmail);
}

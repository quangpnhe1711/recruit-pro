using Microsoft.Extensions.Logging;
using RecruitPro.Application.Interfaces;

namespace RecruitPro.Infrastructure.Service;

public class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;

    /// <summary>
    /// Initializes a new instance of the LoggingEmailService class.
    /// </summary>
    /// <param name="logger">The <paramref name="logger"/> value.</param>
    public LoggingEmailService(ILogger<LoggingEmailService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sends candidate invitation.
    /// </summary>
    /// <param name="email">The <paramref name="email"/> value.</param>
    /// <param name="fullName">The <paramref name="fullName"/> value.</param>
    /// <param name="temporaryPassword">The <paramref name="temporaryPassword"/> value.</param>
    /// <param name="loginUrl">The <paramref name="loginUrl"/> value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SendCandidateInvitationAsync(string email, string fullName, string temporaryPassword, string loginUrl)
    {
        _logger.LogInformation(
            "Candidate invitation queued. Email: {Email}, FullName: {FullName}, TemporaryPassword: {TemporaryPassword}, LoginUrl: {LoginUrl}",
            email,
            fullName,
            temporaryPassword,
            loginUrl);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends password reset.
    /// </summary>
    /// <param name="email">The <paramref name="email"/> value.</param>
    /// <param name="fullName">The <paramref name="fullName"/> value.</param>
    /// <param name="temporaryPassword">The <paramref name="temporaryPassword"/> value.</param>
    /// <param name="loginUrl">The <paramref name="loginUrl"/> value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SendPasswordResetAsync(string email, string fullName, string temporaryPassword, string loginUrl)
    {
        _logger.LogInformation(
            "Password reset queued. Email: {Email}, FullName: {FullName}, TemporaryPassword: {TemporaryPassword}, LoginUrl: {LoginUrl}",
            email,
            fullName,
            temporaryPassword,
            loginUrl);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends an offer email to the candidate. In local/dev this records the send via the logger; the
    /// transition to <c>Offer</c> only proceeds when this completes without throwing.
    /// </summary>
    public Task SendOfferEmailAsync(string email, string fullName, string jobTitle, string subject, string body)
    {
        _logger.LogInformation(
            "Offer email queued. Email: {Email}, FullName: {FullName}, JobTitle: {JobTitle}, Subject: {Subject}",
            email,
            fullName,
            jobTitle,
            subject);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a rejection email to the candidate. In local/dev this records the send via the logger; the
    /// transition to <c>Rejected</c> only proceeds when this completes without throwing.
    /// </summary>
    public Task SendRejectionEmailAsync(string email, string fullName, string jobTitle, string subject, string body)
    {
        _logger.LogInformation(
            "Rejection email queued. Email: {Email}, FullName: {FullName}, JobTitle: {JobTitle}, Subject: {Subject}",
            email,
            fullName,
            jobTitle,
            subject);

        return Task.CompletedTask;
    }
}

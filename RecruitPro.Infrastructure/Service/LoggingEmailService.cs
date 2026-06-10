using Microsoft.Extensions.Logging;
using RecruitPro.Application.Interfaces;

namespace RecruitPro.Infrastructure.Service;

public class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;

    public LoggingEmailService(ILogger<LoggingEmailService> logger)
    {
        _logger = logger;
    }

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
}

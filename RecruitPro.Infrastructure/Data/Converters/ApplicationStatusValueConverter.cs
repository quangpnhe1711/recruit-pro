using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Infrastructure.Data.Converters;

public sealed class ApplicationStatusValueConverter : ValueConverter<ApplicationStatus, string>
{
    public ApplicationStatusValueConverter()
        : base(
            status => status.ToString(),
            value => ParseStatus(value))
    {
    }

    private static ApplicationStatus ParseStatus(string? value)
    {
        string normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;

        return normalized switch
        {
            "applied" or "pending" => ApplicationStatus.Applied,
            "screening" or "hrscreening" or "hr_screening" or "hr-screening" or "reviewing" or "under review" => ApplicationStatus.Screening,
            "managerreview" or "manager_review" or "manager-review" or "final review" => ApplicationStatus.ManagerReview,
            "interview" or "interviewscheduled" or "interview_scheduled" or "interview-scheduled" or "interviewing" => ApplicationStatus.Interview,
            "offer" or "waitingoffer" or "waiting_offer" or "waiting-offer" or "offersent" or "offer_sent" or "offer-sent" or "offered" => ApplicationStatus.Offer,
            "hired" or "accepted" => ApplicationStatus.Hired,
            "rejected" => ApplicationStatus.Rejected,
            "offerdeclined" or "offer_declined" or "offer-declined" or "declined" => ApplicationStatus.OfferDeclined,
            "withdrawn" or "withdraw" or "rút đơn" => ApplicationStatus.Withdrawn,
            _ when Enum.TryParse<ApplicationStatus>(value, true, out ApplicationStatus parsed) => parsed,
            _ => throw new InvalidOperationException(
                $"Cannot convert string value '{value}' from the database to any value in the mapped 'ApplicationStatus' enum.")
        };
    }
}

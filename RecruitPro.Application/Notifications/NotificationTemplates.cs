namespace RecruitPro.Application.Notifications;

/// <summary>
/// Centralized Vietnamese title/body templates for recruitment notifications. Message text lives here
/// (not inline at each call site) so wording stays consistent and is the single place to localize.
/// </summary>
public static class NotificationTemplates
{
    public readonly record struct Message(string Title, string Body);

    public static Message JobSubmittedForApproval(string actorName, string jobTitle) => new(
        "Tin tuyển dụng chờ duyệt",
        $"{actorName} đã gửi tin {jobTitle} để bạn duyệt.");

    public static Message JobApproved(string jobTitle) => new(
        "Tin tuyển dụng đã được duyệt",
        $"Tin {jobTitle} đã được duyệt và có thể hiển thị công khai.");

    public static Message JobRejected(string jobTitle, string? reason) => new(
        "Tin tuyển dụng bị từ chối",
        string.IsNullOrWhiteSpace(reason)
            ? $"Tin {jobTitle} đã bị từ chối."
            : $"Tin {jobTitle} đã bị từ chối. Lý do: {reason}");

    public static Message ApplicationApplied(string candidateName, string jobTitle) => new(
        "Có ứng viên mới",
        $"{candidateName} vừa ứng tuyển vào {jobTitle}.");

    public static Message ApplicationScreeningStarted(string jobTitle) => new(
        "Hồ sơ đang được sàng lọc",
        $"Hồ sơ của bạn cho vị trí {jobTitle} đang được sàng lọc.");

    public static Message DepartmentHeadReviewRequested(string candidateName, string jobTitle) => new(
        "Ứng viên chờ Head Review",
        $"{candidateName} đã qua vòng HR screening cho vị trí {jobTitle}.");

    public static Message InterviewRequested(string candidateName, string jobTitle) => new(
        "Cần xếp lịch phỏng vấn",
        $"{candidateName} đã được duyệt vào vòng phỏng vấn cho {jobTitle}.");

    public static Message InterviewScheduled(string candidateName, string jobTitle) => new(
        "Lịch phỏng vấn mới",
        $"Lịch phỏng vấn cho {candidateName} - {jobTitle} đã được tạo.");

    public static Message InterviewScheduledForCandidate(string jobTitle, string scheduledAt) => new(
        "Lịch phỏng vấn mới",
        $"Bạn có lịch phỏng vấn cho vị trí {jobTitle} vào {scheduledAt}.");

    public static Message InterviewCompleted(string candidateName, string jobTitle) => new(
        "Phỏng vấn đã hoàn thành",
        $"Buổi phỏng vấn của {candidateName} cho {jobTitle} đã hoàn thành.");

    public static Message OfferEmailSent(string candidateName, string jobTitle) => new(
        "Offer đã được gửi",
        $"Offer cho {candidateName} - {jobTitle} đã được gửi.");

    public static Message OfferEmailSentForCandidate(string jobTitle) => new(
        "Bạn nhận được offer",
        $"Bạn vừa nhận được thư mời nhận việc cho vị trí {jobTitle}.");

    public static Message RejectionEmailSent(string candidateName, string jobTitle) => new(
        "Email từ chối đã được gửi",
        $"Email từ chối cho {candidateName} - {jobTitle} đã được gửi.");

    public static Message RejectionEmailSentForCandidate(string jobTitle) => new(
        "Cập nhật hồ sơ ứng tuyển",
        $"Hồ sơ của bạn cho vị trí {jobTitle} đã được cập nhật.");

    public static Message OfferAccepted(string candidateName, string jobTitle) => new(
        "Ứng viên đã chấp nhận offer",
        $"{candidateName} đã chấp nhận offer cho {jobTitle}.");

    public static Message OfferDeclined(string candidateName, string jobTitle) => new(
        "Ứng viên đã từ chối offer",
        $"{candidateName} đã từ chối offer cho {jobTitle}.");

    public static Message ApplicationWithdrawn(string candidateName, string jobTitle) => new(
        "Ứng viên đã rút đơn",
        $"{candidateName} đã rút đơn ứng tuyển {jobTitle}.");
}

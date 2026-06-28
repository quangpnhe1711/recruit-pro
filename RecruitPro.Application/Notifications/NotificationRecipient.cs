namespace RecruitPro.Application.Notifications;

/// <summary>
/// One resolved recipient of a notification together with the role-appropriate deep link to store for
/// them. The same event can produce different <see cref="Url"/> / <see cref="TargetType"/> per recipient
/// (e.g. candidate vs HR vs DepartmentHead) so the link travels with the recipient, not the event.
/// </summary>
public sealed record NotificationRecipient(
    Guid UserId,
    string Url,
    string TargetType,
    Guid? TargetId,
    Guid? SecondaryTargetId = null,
    string? RouteHint = null);

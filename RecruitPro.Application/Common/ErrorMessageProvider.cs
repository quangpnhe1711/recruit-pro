using System.Text.RegularExpressions;

namespace RecruitPro.Application.Common;

/// <summary>
/// Dictionary-backed <see cref="IErrorMessageProvider"/>. Single Vietnamese catalog keyed by the stable
/// codes in <see cref="ErrorCodes"/>. Messages are safe/sanitized (never SQL, stack traces, connection
/// strings, MinIO/raw exception text). Unknown codes fall back to <see cref="ErrorCodes.UnexpectedError"/>.
/// ponytail: a flat dictionary, not .resx/IStringLocalizer — the API is single-locale (vi) for the debug
/// payload and the FE owns real localization. Swap to resources only if the BE must serve multiple locales.
/// </summary>
public sealed class ErrorMessageProvider : IErrorMessageProvider
{
    private static readonly Regex ParamToken = new(@"\{(\w+)\}", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> Vi = new Dictionary<string, string>
    {
        // Generic validation
        [ErrorCodes.ValidationError] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.",
        [ErrorCodes.ValidationFailed] = "Vui lòng kiểm tra lại thông tin đã nhập.",
        [ErrorCodes.FormInvalid] = "Vui lòng kiểm tra lại thông tin đã nhập.",
        [ErrorCodes.InvalidInput] = "Dữ liệu gửi lên không hợp lệ.",
        [ErrorCodes.Required] = "Trường này là bắt buộc.",
        [ErrorCodes.InvalidEmail] = "Email không hợp lệ.",
        [ErrorCodes.InvalidFormat] = "Định dạng không hợp lệ.",
        [ErrorCodes.MaxLengthExceeded] = "Không được vượt quá {maxLength} ký tự.",
        [ErrorCodes.MinLengthRequired] = "Phải có ít nhất {minLength} ký tự.",
        [ErrorCodes.OutOfRange] = "Giá trị nằm ngoài khoảng cho phép.",
        [ErrorCodes.InvalidAmount] = "Số tiền không hợp lệ.",

        // Account / credentials
        [ErrorCodes.EmailAlreadyExists] = "Email này đã được sử dụng.",
        [ErrorCodes.UsernameAlreadyExists] = "Tên đăng nhập này đã tồn tại.",
        [ErrorCodes.PasswordTooWeak] = "Mật khẩu chưa đủ mạnh.",
        [ErrorCodes.PasswordTooShort] = "Mật khẩu phải có ít nhất {minLength} ký tự.",
        [ErrorCodes.InvalidCredentials] = "Tài khoản hoặc mật khẩu không đúng.",
        [ErrorCodes.TokenExpired] = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
        [ErrorCodes.Unauthenticated] = "Vui lòng đăng nhập để tiếp tục.",
        [ErrorCodes.Forbidden] = "Bạn không có quyền thực hiện thao tác này.",
        [ErrorCodes.PortalAccessDenied] = "Tài khoản không có quyền truy cập cổng này.",
        [ErrorCodes.AccountDisabled] = "Tài khoản đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên hệ thống.",

        // Not-found family
        [ErrorCodes.EntityNotFound] = "Không tìm thấy dữ liệu.",
        [ErrorCodes.JobNotFound] = "Không tìm thấy tin tuyển dụng.",
        [ErrorCodes.CandidateNotFound] = "Không tìm thấy ứng viên.",
        [ErrorCodes.CandidateProfileNotFound] = "Không tìm thấy hồ sơ ứng viên.",
        [ErrorCodes.UserNotFound] = "Không tìm thấy người dùng.",
        [ErrorCodes.InterviewNotFound] = "Không tìm thấy lịch phỏng vấn.",
        [ErrorCodes.ResumeNotFound] = "Không tìm thấy CV.",
        [ErrorCodes.RoleNotFound] = "Không tìm thấy vai trò.",
        [ErrorCodes.CandidateRoleNotFound] = "Không tìm thấy vai trò ứng viên.",
        [ErrorCodes.ExperienceNotFound] = "Không tìm thấy thông tin kinh nghiệm.",
        [ErrorCodes.ApplicationNotFound] = "Không tìm thấy hồ sơ ứng tuyển.",
        [ErrorCodes.NotificationNotFound] = "Không tìm thấy thông báo.",
        [ErrorCodes.WorkflowNotFound] = "Không tìm thấy quy trình tự động.",
        [ErrorCodes.WorkflowExecutionNotFound] = "Không tìm thấy lần chạy quy trình.",
        [ErrorCodes.McpToolNotFound] = "Không tìm thấy công cụ.",
        [ErrorCodes.OfferNotFound] = "Không tìm thấy offer.",
        [ErrorCodes.DepartmentNotFound] = "Không tìm thấy phòng ban.",
        [ErrorCodes.RbacRoleNotFound] = "Không tìm thấy vai trò.",

        // Conflict / duplicate
        [ErrorCodes.Conflict] = "Thao tác xung đột với dữ liệu hiện có.",
        [ErrorCodes.DuplicateEntity] = "Dữ liệu đã tồn tại.",
        [ErrorCodes.ApplicationAlreadyActive] = "Bạn đang có một đơn ứng tuyển còn hiệu lực cho vị trí này.",
        [ErrorCodes.ApplicationAlreadyHired] = "Bạn đã được tuyển cho vị trí này nên không thể ứng tuyển lại.",

        // Business rules (generic + application/job/interview/offer domain)
        [ErrorCodes.BusinessRuleViolation] = "Không thể thực hiện thao tác này ở trạng thái hiện tại.",
        [ErrorCodes.InvalidStatusTransition] = "Không thể chuyển sang trạng thái này.",
        [ErrorCodes.JobClosed] = "Tin tuyển dụng đã đóng.",
        [ErrorCodes.SalaryRangeInvalid] = "Mức lương tối đa phải lớn hơn mức lương tối thiểu.",
        [ErrorCodes.DateMustBeInFuture] = "Ngày phải nằm trong tương lai.",
        [ErrorCodes.InterviewTimeInvalid] = "Thời gian phỏng vấn không hợp lệ.",
        [ErrorCodes.InterviewTimeInPast] = "Thời gian phỏng vấn không được nằm trong quá khứ.",
        [ErrorCodes.OfferAlreadySent] = "Offer đã được gửi trước đó.",
        [ErrorCodes.JobNotAcceptingApplications] = "Vị trí này hiện không nhận hồ sơ mới.",
        [ErrorCodes.JobDeadlinePassed] = "Đã hết hạn nộp hồ sơ cho vị trí này.",
        [ErrorCodes.CandidateProfileIncomplete] = "Hồ sơ của bạn còn thiếu thông tin liên hệ bắt buộc.",
        [ErrorCodes.ResumeRequired] = "Vui lòng tải lên CV mới nhất trước khi ứng tuyển.",
        [ErrorCodes.ApplicationNotWithdrawable] = "Đơn ứng tuyển này không thể rút lại ở trạng thái hiện tại.",
        [ErrorCodes.InvalidApplicationTransition] = "Thao tác chuyển trạng thái không hợp lệ.",
        [ErrorCodes.InterviewNotActionable] = "Không thể thao tác phỏng vấn ở trạng thái hiện tại của hồ sơ.",
        [ErrorCodes.OfferNotActionable] = "Offer hiện không ở trạng thái có thể phản hồi.",
        [ErrorCodes.InterviewRequired] = "Hãy lên lịch phỏng vấn trước khi gửi offer hoặc từ chối.",
        [ErrorCodes.InterviewNotCompleted] = "Hãy hoàn tất phỏng vấn trước khi gửi offer hoặc từ chối.",
        [ErrorCodes.EmailRequiredForOffer] = "Vui lòng gửi email offer thay vì đổi trạng thái trực tiếp.",
        [ErrorCodes.EmailRequiredForRejection] = "Vui lòng gửi email từ chối thay vì đổi trạng thái trực tiếp.",
        [ErrorCodes.EmailSendFailed] = "Không gửi được email; trạng thái hồ sơ chưa thay đổi.",
        [ErrorCodes.DepartmentHeadRequired] = "Phòng ban này chưa có trưởng bộ phận nên chưa thể duyệt tin tuyển dụng.",
        [ErrorCodes.InvalidDepartmentHead] = "Người được chọn làm trưởng bộ phận phải là tài khoản có vai trò Trưởng bộ phận.",
        [ErrorCodes.JobRecruiterRequired] = "Tin tuyển dụng cần có người phụ trách (recruiter) trước khi thực hiện thao tác này.",
        [ErrorCodes.InvalidJobRecruiter] = "Người phụ trách được chọn phải là tài khoản có vai trò HR.",
        [ErrorCodes.InvalidJobTransition] = "Không thể chuyển tin tuyển dụng sang trạng thái này.",
        [ErrorCodes.JobDeadlineInvalid] = "Hạn nộp hồ sơ không hợp lệ.",

        // System Admin / RBAC
        [ErrorCodes.RbacUnknownPermission] = "Quyền được chọn không tồn tại.",
        [ErrorCodes.RbacAdminLockout] = "Không thể tự gỡ bỏ quyền quản trị của chính mình.",
        [ErrorCodes.UserStatusInvalid] = "Trạng thái người dùng không hợp lệ.",
        [ErrorCodes.UserSelfDeactivation] = "Không thể tự vô hiệu hóa tài khoản của chính mình.",

        // File / upload
        [ErrorCodes.FileRequired] = "Vui lòng chọn file.",
        [ErrorCodes.FileTooLarge] = "File vượt quá dung lượng cho phép.",
        [ErrorCodes.UnsupportedFileType] = "Định dạng file không được hỗ trợ.",
        [ErrorCodes.FileUploadFailed] = "Tải file lên thất bại. Vui lòng thử lại.",
        [ErrorCodes.ResumeFileRequired] = "Vui lòng chọn file CV.",
        [ErrorCodes.ResumeFileEmpty] = "File CV rỗng. Vui lòng chọn file khác.",
        [ErrorCodes.ResumeFileTooLarge] = "File CV vượt quá dung lượng cho phép.",
        [ErrorCodes.ResumeFileUnsupportedType] = "Định dạng CV không được hỗ trợ.",

        // AI / provider
        [ErrorCodes.AiProviderUnavailable] = "Dịch vụ AI hiện không khả dụng. Vui lòng thử lại sau.",
        [ErrorCodes.AiProcessingFailed] = "Xử lý AI thất bại. Vui lòng thử lại.",

        // System / transport
        [ErrorCodes.ServerError] = "Hệ thống đang gặp lỗi. Vui lòng thử lại sau.",
        [ErrorCodes.UnexpectedError] = "Đã có lỗi xảy ra. Vui lòng thử lại.",
        [ErrorCodes.NetworkError] = "Không thể kết nối tới máy chủ.",
        [ErrorCodes.TimeoutError] = "Yêu cầu mất quá nhiều thời gian. Vui lòng thử lại.",
    };

    public string GetMessage(string code, IReadOnlyDictionary<string, object?>? parameters = null, string? locale = null)
    {
        string template = Vi.TryGetValue(code, out string? message)
            ? message
            : Vi[ErrorCodes.UnexpectedError];

        if (parameters is null || parameters.Count == 0)
        {
            return template;
        }

        return ParamToken.Replace(template, match =>
            parameters.TryGetValue(match.Groups[1].Value, out object? value)
                ? value?.ToString() ?? string.Empty
                : match.Value);
    }
}

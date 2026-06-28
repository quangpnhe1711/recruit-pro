using Microsoft.Extensions.Logging;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Request.Offers;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using RecruitPro.Domain.Workflows;

namespace RecruitPro.Application.Services;

public class OfferService : IOfferService
{
    private static readonly string[] ReportingManagerRoles = ["HR", "Manager"];

    private readonly IApplicationRepository _applicationRepository;
    private readonly IOfferRepository _offerRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly INotificationEventService _notificationEventService;
    private readonly ILogger<OfferService> _logger;

    /// <summary>
    /// Initializes a new instance of the OfferService class.
    /// </summary>
    /// <param name="applicationRepository">The <paramref name="applicationRepository"/> value.</param>
    /// <param name="offerRepository">The <paramref name="offerRepository"/> value.</param>
    /// <param name="userRepository">The <paramref name="userRepository"/> value.</param>
    /// <param name="unitOfWork">The <paramref name="unitOfWork"/> value.</param>
    /// <param name="emailService">The <paramref name="emailService"/> value.</param>
    public OfferService(
        IApplicationRepository applicationRepository,
        IOfferRepository offerRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        INotificationEventService notificationEventService,
        ILogger<OfferService> logger)
    {
        _applicationRepository = applicationRepository;
        _offerRepository = offerRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _notificationEventService = notificationEventService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves offer editor.
    /// </summary>
    /// <param name="applicationId">The <paramref name="applicationId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<ApplicationOfferEditorDto>> GetOfferEditorAsync(string applicationId)
    {
        Domain.Entities.Application? application = await GetApplicationAsync(applicationId);
        if (application == null)
        {
            return ApiResponse<ApplicationOfferEditorDto>.NotFound("Không tìm thấy hồ sơ ứng tuyển.");
        }

        ApplicationOffer? offer = await _offerRepository.GetByApplicationIdAsync(application.Id);
        return ApiResponse<ApplicationOfferEditorDto>.Ok(await BuildEditorDtoAsync(application, offer));
    }

    /// <summary>
    /// Saves draft.
    /// </summary>
    /// <param name="applicationId">The <paramref name="applicationId"/> value.</param>
    /// <param name="actorId">The <paramref name="actorId"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<ApplicationOfferEditorDto>> SaveDraftAsync(string applicationId, Guid? actorId, UpsertApplicationOfferRequest request)
    {
        _ = actorId;
        return await UpsertOfferAsync(applicationId, actorId, request, OfferStatus.Draft, "Đã lưu nháp offer.");
    }

    /// <summary>
    /// Sends offer.
    /// </summary>
    /// <param name="applicationId">The <paramref name="applicationId"/> value.</param>
    /// <param name="actorId">The <paramref name="actorId"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<ApplicationOfferEditorDto>> SendOfferAsync(string applicationId, Guid? actorId, UpsertApplicationOfferRequest request)
    {
        _ = actorId;
        return await UpsertOfferAsync(applicationId, actorId, request, OfferStatus.Sent, "Gửi offer thành công.");
    }

    /// <summary>
    /// Executes the upsert offer operation.
    /// </summary>
    /// <param name="applicationId">The <paramref name="applicationId"/> value.</param>
    /// <param name="actorId">The <paramref name="actorId"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <param name="targetStatus">The <paramref name="targetStatus"/> value.</param>
    /// <param name="successMessage">The <paramref name="successMessage"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    private async Task<ApiResponse<ApplicationOfferEditorDto>> UpsertOfferAsync(
        string applicationId,
        Guid? actorId,
        UpsertApplicationOfferRequest request,
        OfferStatus targetStatus,
        string successMessage)
    {
        _ = actorId;
        Domain.Entities.Application? application = await GetApplicationAsync(applicationId, tracked: true);
        if (application == null)
        {
            return ApiResponse<ApplicationOfferEditorDto>.NotFound("Không tìm thấy hồ sơ ứng tuyển.");
        }

        // BR-WF-004: an offer can be prepared while the application is already at the Offer stage
        // (edit / re-send) or while it is in the Interview stage (the first send). It must NOT be
        // prepared from any earlier stage.
        bool isOfferStage = ApplicationStatusWorkflow.CanPrepareOffer(application.Status); // Offer/Hired/OfferDeclined
        bool isInterviewStage = application.Status == ApplicationStatus.Interview;
        if (!isOfferStage && !isInterviewStage)
        {
            return ApiResponse<ApplicationOfferEditorDto>.UnprocessableEntity(
                "Chỉ hồ sơ ở bước phỏng vấn đã hoàn tất hoặc bước offer mới có thể soạn offer.",
                errorCode: ErrorCodes.OfferNotActionable);
        }

        // BR-WF-005: preparing/sending an offer from the Interview stage requires a scheduled AND
        // completed interview. (When the application is already in Offer this gate does not apply.)
        if (isInterviewStage)
        {
            switch (InterviewWorkflow.EvaluateCompletion(application.Interviews))
            {
                case InterviewCompletionState.Required:
                    return ApiResponse<ApplicationOfferEditorDto>.UnprocessableEntity(
                        "Hãy lên lịch phỏng vấn trước khi gửi offer.",
                        errorCode: ErrorCodes.InterviewRequired);
                case InterviewCompletionState.NotCompleted:
                    return ApiResponse<ApplicationOfferEditorDto>.UnprocessableEntity(
                        "Hãy hoàn tất phỏng vấn trước khi gửi offer.",
                        errorCode: ErrorCodes.InterviewNotCompleted);
            }
        }

        ApplicationOffer? offer = await _offerRepository.GetTrackedByApplicationIdAsync(application.Id);
        bool isNew = offer == null;

        if (!await CurrencyExistsAsync(request.CurrencyCode))
        {
            return ApiResponse<ApplicationOfferEditorDto>.BadRequest("Loại tiền tệ đã chọn không hợp lệ.");
        }

        if (!string.IsNullOrWhiteSpace(request.OfferTemplateId) &&
            !Guid.TryParse(request.OfferTemplateId, out Guid offerTemplateId))
        {
            return ApiResponse<ApplicationOfferEditorDto>.BadRequest("Mẫu offer không hợp lệ.");
        }

        Guid? reportingManagerId = null;
        if (!string.IsNullOrWhiteSpace(request.ReportingManagerId))
        {
            if (!Guid.TryParse(request.ReportingManagerId, out Guid parsedManagerId))
            {
                return ApiResponse<ApplicationOfferEditorDto>.BadRequest("Người quản lý báo cáo không hợp lệ.");
            }

            reportingManagerId = parsedManagerId;
        }

        IReadOnlyList<OfferBenefit> availableBenefits = await _offerRepository.GetBenefitsAsync();
        HashSet<Guid> availableBenefitIds = availableBenefits.Select(benefit => benefit.Id).ToHashSet();
        List<Guid> benefitIds = new();
        foreach (string rawId in request.BenefitIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(rawId, out Guid benefitId))
            {
                return ApiResponse<ApplicationOfferEditorDto>.BadRequest("Có phúc lợi không hợp lệ.");
            }

             if (!availableBenefitIds.Contains(benefitId))
            {
                return ApiResponse<ApplicationOfferEditorDto>.BadRequest("Có phúc lợi hiện không khả dụng.");
            }

            benefitIds.Add(benefitId);
        }

        if (offer == null)
        {
            offer = new ApplicationOffer
            {
                Id = Guid.NewGuid(),
                ApplicationId = application.Id,
                CreatedAt = DbDateTime.Now
            };
        }

        offer.OfferTemplateId = Guid.TryParse(request.OfferTemplateId, out Guid templateId) ? templateId : null;
        offer.BaseSalary = request.BaseSalary;
        offer.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        offer.BonusDescription = TextNormalizationHelper.NormalizeOptionalText(request.BonusDescription);
        offer.EquityNotes = TextNormalizationHelper.NormalizeOptionalText(request.EquityNotes);
        offer.EmploymentType = request.EmploymentType.Trim();
        offer.ProposedStartDate = request.ProposedStartDate;
        offer.ProbationPeriod = TextNormalizationHelper.NormalizeOptionalText(request.ProbationPeriod);
        offer.ReportingManagerId = reportingManagerId;
        offer.PersonalMessage = TextNormalizationHelper.NormalizeOptionalText(request.PersonalMessage);
        offer.Status = targetStatus;
        offer.SentAt = targetStatus == OfferStatus.Sent ? DbDateTime.Now : offer.SentAt;
        offer.UpdatedAt = DbDateTime.Now;

        // BR-WF-001: only the Send path (the offer email) moves the application into Offer. Saving a
        // draft leaves the application in its current stage (e.g. Interview). The transition happens only
        // after the offer email is sent successfully — a send failure leaves the application untouched.
        if (targetStatus == OfferStatus.Sent)
        {
            string subject = $"Thư mời nhận việc - {application.Job.Title}";
            string body = string.IsNullOrWhiteSpace(offer.PersonalMessage)
                ? $"Chúc mừng! Chúng tôi trân trọng gửi tới bạn thư mời nhận việc cho vị trí {application.Job.Title}."
                : offer.PersonalMessage!;

            try
            {
                await _emailService.SendOfferEmailAsync(
                    application.User.Email,
                    application.User.FullName,
                    application.Job.Title,
                    subject,
                    body);
            }
            catch (Exception)
            {
                return ApiResponse<ApplicationOfferEditorDto>.UnprocessableEntity(
                    "Không gửi được email offer; trạng thái hồ sơ chưa thay đổi.",
                    errorCode: ErrorCodes.EmailSendFailed);
            }

            application.Status = ApplicationStatus.Offer;
        }

        SynchronizeBenefits(offer, benefitIds);

        await _unitOfWork.BeginTransactionAsync();

        if (isNew)
        {
            await _offerRepository.AddAsync(offer);
        }
        else
        {
            await _offerRepository.UpdateAsync(offer);
        }

        await _applicationRepository.UpdateAsync(application);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitAsync();

        ApplicationOffer? refreshedOffer = await _offerRepository.GetByApplicationIdAsync(application.Id);
        Domain.Entities.Application? refreshedApplication = await GetApplicationAsync(applicationId);

        if (refreshedApplication == null || refreshedOffer == null)
        {
            return ApiResponse<ApplicationOfferEditorDto>.NotFound("Không tải lại được dữ liệu offer.");
        }

        // offer_email_sent: only after the offer email succeeded AND the application transitioned to
        // Offer. The notification does NOT replace the email — it is a best-effort post-commit side
        // effect that must never fail the committed send.
        if (targetStatus == OfferStatus.Sent)
        {
            try
            {
                await _notificationEventService.PublishOfferEmailSentAsync(refreshedApplication, refreshedOffer);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Offer email for application {ApplicationId} was sent but the offer-email notification failed to publish.",
                    refreshedApplication.Id);
            }
        }

        return ApiResponse<ApplicationOfferEditorDto>.Ok(
            await BuildEditorDtoAsync(refreshedApplication, refreshedOffer),
            successMessage);
    }

    /// <summary>
    /// Retrieves application.
    /// </summary>
    /// <param name="applicationId">The <paramref name="applicationId"/> value.</param>
    /// <param name="tracked">The <paramref name="tracked"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    private async Task<Domain.Entities.Application?> GetApplicationAsync(string applicationId, bool tracked = false)
    {
        if (!Guid.TryParse(applicationId, out Guid applicationGuid))
        {
            return null;
        }

        return tracked
            ? await _applicationRepository.GetTrackedByIdAsync(applicationGuid)
            : await _applicationRepository.GetByIdAsync(applicationGuid);
    }

    /// <summary>
    /// Executes the currency exists operation.
    /// </summary>
    /// <param name="currencyCode">The <paramref name="currencyCode"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    private async Task<bool> CurrencyExistsAsync(string currencyCode)
    {
        string normalizedCode = currencyCode.Trim().ToUpperInvariant();
        IReadOnlyList<OfferCurrency> currencies = await _offerRepository.GetCurrenciesAsync();
        return currencies.Any(currency => currency.Code.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Executes the synchronize benefits operation.
    /// </summary>
    /// <param name="offer">The <paramref name="offer"/> value.</param>
    /// <param name="benefitIds">The <paramref name="benefitIds"/> value.</param>
    private static void SynchronizeBenefits(ApplicationOffer offer, IEnumerable<Guid> benefitIds)
    {
        HashSet<Guid> targetIds = benefitIds.ToHashSet();
        List<ApplicationOfferBenefit> removedItems = offer.ApplicationOfferBenefits
            .Where(link => !targetIds.Contains(link.BenefitId))
            .ToList();

        foreach (ApplicationOfferBenefit removedItem in removedItems)
        {
            offer.ApplicationOfferBenefits.Remove(removedItem);
        }

        HashSet<Guid> existingIds = offer.ApplicationOfferBenefits
            .Select(link => link.BenefitId)
            .ToHashSet();

        foreach (Guid benefitId in targetIds)
        {
            if (existingIds.Contains(benefitId))
            {
                continue;
            }

            offer.ApplicationOfferBenefits.Add(new ApplicationOfferBenefit
            {
                OfferId = offer.Id,
                BenefitId = benefitId
            });
        }
    }

    /// <summary>
    /// Builds editor dto.
    /// </summary>
    /// <param name="application">The <paramref name="application"/> value.</param>
    /// <param name="offer">The <paramref name="offer"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    private async Task<ApplicationOfferEditorDto> BuildEditorDtoAsync(Domain.Entities.Application application, ApplicationOffer? offer)
    {
        IReadOnlyList<OfferTemplate> templates = await _offerRepository.GetTemplatesAsync();
        IReadOnlyList<OfferBenefit> benefits = await _offerRepository.GetBenefitsAsync();
        IReadOnlyList<OfferCurrency> currencies = await _offerRepository.GetCurrenciesAsync();
        IReadOnlyList<User> reportingManagers = await _userRepository.GetUsersInRolesAsync(ReportingManagerRoles);

        string defaultCurrencyCode = currencies.FirstOrDefault()?.Code ?? "VND";
        OfferTemplate? defaultTemplate = templates.FirstOrDefault();
        User? defaultManager = reportingManagers.FirstOrDefault();

        return new ApplicationOfferEditorDto
        {
            Application = new OfferEditorApplicationSummaryDto
            {
                ApplicationId = application.Id.ToString(),
                ReferenceCode = $"APP-{application.Id.ToString("N")[..8].ToUpperInvariant()}",
                StageLabel = application.Status switch
                {
                    ApplicationStatus.Applied => "Applied",
                    ApplicationStatus.Screening => "Screening",
                    ApplicationStatus.ManagerReview => "Manager Review",
                    ApplicationStatus.Interview => "Interview",
                    ApplicationStatus.Offer => "Offer",
                    ApplicationStatus.Hired => "Hired",
                    ApplicationStatus.Rejected => "Rejected",
                    ApplicationStatus.OfferDeclined => "Offer Declined",
                    _ => application.Status.ToString()
                },
                CandidateName = application.User.FullName,
                CandidateEmail = application.User.Email,
                CandidateAvatarUrl = application.User.AvatarUrl,
                JobTitle = application.Job.Title,
                DepartmentName = application.Job.Department?.Name ?? "RecruitPro"
            },
            Offer = new ApplicationOfferDetailDto
            {
                OfferId = offer?.Id.ToString(),
                Status = offer?.Status.ToString() ?? OfferStatus.Draft.ToString(),
                OfferTemplateId = offer?.OfferTemplateId?.ToString() ?? defaultTemplate?.Id.ToString(),
                BaseSalary = offer?.BaseSalary ?? 0,
                CurrencyCode = offer?.CurrencyCode ?? defaultCurrencyCode,
                BonusDescription = offer?.BonusDescription,
                EquityNotes = offer?.EquityNotes,
                EmploymentType = offer?.EmploymentType ?? application.Job.EmploymentType.ToString(),
                ProposedStartDate = offer?.ProposedStartDate,
                ProbationPeriod = offer?.ProbationPeriod ?? "2 Months",
                ReportingManagerId = offer?.ReportingManagerId?.ToString() ?? defaultManager?.Id.ToString(),
                ReportingManagerName = offer?.ReportingManagerNavigation?.FullName ?? defaultManager?.FullName,
                PersonalMessage = offer?.PersonalMessage,
                BenefitIds = offer?.ApplicationOfferBenefits
                    .Select(link => link.BenefitId.ToString())
                    .ToList() ?? [],
                SentAt = offer?.SentAt,
                UpdatedAt = offer?.UpdatedAt
            },
            MasterData = new OfferMasterDataDto
            {
                Templates = templates.Select(template => new OfferTemplateOptionDto
                {
                    Id = template.Id.ToString(),
                    Name = template.Name,
                    Description = template.Description
                }).ToList(),
                Benefits = benefits.Select(benefit => new OfferBenefitOptionDto
                {
                    Id = benefit.Id.ToString(),
                    Name = benefit.Name,
                    Description = benefit.Description
                }).ToList(),
                Currencies = currencies.Select(currency => new OfferCurrencyOptionDto
                {
                    Code = currency.Code,
                    Name = currency.Name,
                    Symbol = currency.Symbol
                }).ToList(),
                EmploymentTypes =
                [
                    EmploymentType.FullTime.ToString().Replace("FullTime", "Full-time"),
                    EmploymentType.PartTime.ToString().Replace("PartTime", "Part-time"),
                    EmploymentType.Internship.ToString(),
                    EmploymentType.Contract.ToString()
                ],
                ReportingManagers = reportingManagers.Select(user => new OfferReportingManagerOptionDto
                {
                    Id = user.Id.ToString(),
                    FullName = user.FullName,
                    Email = user.Email,
                    Title = user.UserRoles.Select(role => role.Role.Name).FirstOrDefault() ?? "Internal"
                }).ToList()
            }
        };
    }

}

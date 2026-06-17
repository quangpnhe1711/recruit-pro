# Refactor Candidates For Shared Common Layer

## Purpose

This note collects helper methods and patterns that appear repeatedly across backend services
or are large enough to justify extraction into shared/common components.

## High-Value Duplicates

- `BuildMeta(...)`
  Appears in multiple services such as `ApplicationService`, `CandidateService`, `InterviewService`, and `JobService`.
  Suggested extraction: `PaginationMetaBuilder` or `ApiEnvelopeFactory`.

- `NormalizeOptionalText(...)`
  Appears in at least `CandidateService` and `OfferService`.
  Suggested extraction: `StringNormalizationHelper`.

- `GenerateTemporaryPassword(...)`
  Appears in both `AuthService` and `CandidateService`.
  Suggested extraction: `CredentialUtility` or `TemporaryPasswordGenerator`.

- `GetProfileEntityAsync(...)`
  Similar candidate-profile lookup logic exists in `CandidateService`, `ApplicationService`, and `DashboardService`.
  Suggested extraction: `CandidateProfileAccessor`.

- `GetJobAsync(...)`
  Job lookup-and-throw pattern appears in more than one service context.
  Suggested extraction: `JobAccessor` or domain query helper.

- `ExtractFileName(...)`
  Resume/file-name extraction helpers exist in both `ApplicationService` and `CandidateService`.
  Suggested extraction: `StoredFileNameHelper`.

- `BuildSalaryLabel(...)`
  Salary formatting logic exists in `ApplicationService` and `JobService`.
  Suggested extraction: `CompensationLabelHelper`.

## OpenAI-Compatible Provider Duplication

The two infrastructure AI providers share a very similar protocol adapter layer:

- `BuildRequest(...)`
- `BuildRequestBody(...)`
- `BuildEndpoint(...)`
- `UsesChatCompletions()`
- `ExtractOutputText(...)`
- `NormalizeJsonPayload(...)`

Files involved:
- `OpenAiCopilotProvider`
- `OpenAiResumeParserProvider`

Suggested extraction:
- `OpenAiCompatibleClientHelper`
- or abstract base class for OpenAI-compatible providers

Benefit:
- one place to handle provider compatibility quirks
- lower risk of divergence when request/response formats change

## CandidateService Split Candidates

`CandidateService` is doing several jobs at once:

- candidate registration
- profile CRUD
- candidate import
- resume upload
- AI resume parsing
- fallback resume parsing

Suggested decomposition:

- `CandidateRegistrationService`
- `CandidateImportService`
- `CandidateProfileService`
- `CandidateResumeService`
- `ResumeParsingService`

Benefit:
- lower class size
- easier testing
- clearer ownership boundaries

## JobService Split Candidates

`JobService` currently mixes:

- public job browse/search
- HR job CRUD
- manager approval review
- DTO mapping

Suggested decomposition:

- `PublicJobQueryService`
- `HrJobManagementService`
- `ManagerJobApprovalService`

## ApplicationService Split Candidates

`ApplicationService` currently mixes:

- candidate apply flow
- candidate self-service actions
- HR listings
- manager review queue
- review detail mapping
- offer-response transitions

Suggested decomposition:

- `CandidateApplicationService`
- `HrApplicationReviewService`
- `ManagerApplicationDecisionService`

## Suggested Order Of Refactor

1. Extract duplicated pure helpers first
2. Extract shared OpenAI-compatible provider helper
3. Split `CandidateService`
4. Split `JobService`
5. Split `ApplicationService`

This order gives the best value with the lowest migration risk.

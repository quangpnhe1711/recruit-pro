# Test Matrix — Application Domain

All tests below are in `RecruitPro.Tests` and run under `dotnet test` (xUnit + Moq + FluentAssertions;
integration via Testcontainers PostgreSQL). Full suite at time of writing: **138 passed, 0 failed**.

| Test ID | Test | Layer | Rule / Bug | Expected |
|---|---|---|---|---|
| T-WD-001 | `WithdrawApplicationAsync_WhenAllowed_SetsStatusToWithdrawnNotRejected` | unit | BR-003 / BUG-001 | status → `Withdrawn` (not `Rejected`), 200 |
| T-WD-002 | `WithdrawApplicationAsync_WhenAlreadyClosed_Returns422` | unit | BR-003 | 422, status unchanged |
| T-RE-001 | `ApplyAsync_AfterWithdrawal_AllowsReapplyAndReturnsCreated` | unit | BR-002 / BUG-001 | 201, `AddAsync` called once |
| T-RE-002 | `ApplyAsync_AfterRejection_AllowsReapplyAndReturnsCreated` | unit | BR-002 | 201 |
| T-DUP-001 | `ApplyAsync_WhenActiveApplicationExists_Returns409Conflict` (5 states) | unit | BR-001 | 409, no insert |
| T-DUP-002 | `ApplyAsync_WhenCandidateAlreadyApplied_ReturnsConflict` | unit | BR-001 | 409 + exact message |
| T-MSG-001 | `ApplyAsync_WhenBlockedByNonDuplicateReason_DoesNotClaimAlreadyApplied` | unit | BR-004 / BUG-001 | 422, message ≠ "already applied" |
| T-500-001 | `ApplyAsync_WhenNotificationPublishFails_StillReturnsCreated` | unit | BR-005 / BUG-002 | 201 despite notification throw |
| T-500-002 | `ApplyAsync_WhenSemanticEnqueueFails_StillReturnsCreated` | unit | BR-005 / BUG-002 | 201 despite enqueue throw |
| T-NOTI-001 | `PublishNewApplicationReceivedAsync_FansOutToHrAndDeduplicatesRecipients` | unit | BR-005 | notify `job.CreatedBy` + role `HR`, no duplicates |
| T-CTX-001 | `GetApplyScreenAsync_AfterWithdrawal_ReportsCanApplyAndNotAlreadyApplied` | unit | BR-001/002 | canApply=true, alreadyApplied=false |
| T-WF-001 | `ApplicationStatusWorkflowTests.IsClosed_DetectsTerminalStates` (incl. Withdrawn) | unit | DL-002 / BR-007 | closed/active is derived from status |
| T-WF-002 | `ApplicationStatusWorkflowTests.CanCandidateWithdraw_MatchesPolicy` (incl. Withdrawn/Rejected) | unit | BR-003 / BR-007 | candidate actions depend on status |
| T-WF-003 | `ApplicationStatusWorkflowTests.CanTransition_RespectsConfiguredWorkflow` | unit | BR-006 / BR-007 | reviewer transitions depend on previous status |
| T-CV-001 | `ApplicationStatusValueConverter_MapsLegacyStrings` (incl. `withdrawn`/`Withdrawn`) | unit | DL-002 | round-trips Withdrawn |
| T-E2E-001 | `ApplicationService_WithdrawThenReapply_SucceedsWithoutConflictOr500` | integration (PG) | BUG-001/002 | withdraw 200 → reapply 201 → duplicate 409 |
| T-API-001 | `ApplicationController_ApplyContext_Should_Require_Candidate…` | integration (PG) | authz | candidate 200, HR-as-candidate 403 |
| T-API-002 | `Public_Apis_Should_Return_Controlled_404_Not_500_For_Invalid_Ids` | integration (PG) | 500 eradication | 404 not 500 |

## Implemented in the conformance pass (DL-007/008/009)

Full suite after this pass: **156 passed, 0 failed** (was 138). New/updated tests:

| Test ID | Test | Layer | Rule / Inv | Expected |
|---|---|---|---|---|
| T-RE-003 | `ApplicationConformanceTests.ApplyAsync_AfterHired_ForSameJob_IsBlocked` | unit | BR-002 / INV-015 | re-apply after `Hired` same job → 422 `APPLICATION_ALREADY_HIRED`, no insert |
| T-DUP-003 | `ApplicationConformanceTests.ApplyAsync_UsesExistsActive_NotArbitraryRow` | unit | BR-001 / INV-003 | active detected via EXISTS query even when fetched row is closed → 409 |
| T-DUP-004 | `ServiceIntegrationTests.ConcurrentApply_AllowsOnlyOneActiveApplication` | integration (PG, **truly concurrent**) | INV-014 | two parallel applies → exactly one 201, one 409, exactly one active row in DB |
| INV-014-DB | `RepositoryIntegrationTests.ActiveApplicationUniqueIndex_*` (reject active dup / allow closed dup) | integration (PG) | INV-014 | partial unique index rejects 2nd active row; allows closed duplicate |
| T-OFR-001 | `AcceptOffer_WhenNotInOfferStage_Returns422`, `AcceptOffer_WhenOfferNotSent_Returns422`, `UpdateApplicationDecision_FromOffer_IsRejected` | unit | BR-009 / INV-009 | forbidden offer combos & reviewer Offer→Hired → 422 |
| T-OFR-002 | `AcceptOffer_WhenOfferSent_SetsOfferAcceptedAndApplicationHired` | unit | INV-009 | Offer Accepted + Application Hired, 200 |
| T-OFR-003 | `DeclineOffer_WhenOfferSent_SetsOfferDeclinedAndApplicationOfferDeclined` | unit | INV-009 | Offer Declined + Application OfferDeclined, 200 |
| T-INT-001 | `CreateInterview_WhenApplicationNotInInterviewStage_Returns422` | unit | BR-008 / INV-008 | off-stage interview create → 422 `INTERVIEW_NOT_ACTIONABLE` |
| T-INT-002 | `Withdraw_FromInterviewStage_CancelsPendingInterview` | unit | BR-008 / INV-008 | pending `Scheduled` → `Canceled`; completed untouched |
| T-WF-004 | `ApplicationStatusWorkflowTests.IsReapplyEligibleClosedStatus_ExcludesHiredAndActiveStates` | unit | INV-015 | Rejected/Withdrawn/OfferDeclined eligible; Hired not |

## Still planned (not in this pass)

| Test ID | Intended test | Rule / Invariant | Notes |
|---|---|---|---|
| T-DASH-001 | `Analytics_DeriveFromCanonicalGroups_WithdrawnNotRejected` | BR-011 / INV-011 | INV-011 already PASS in code; explicit analytics assertion deferred |

## Coverage evidence (cobertura, this run)

| Class / method | Line | Branch |
|---|---|---|
| `ApplicationService.ApplyAsync` | 100% | 87.5% |
| `ApplicationService.WithdrawApplicationAsync` | 100% | 100% |
| `ApplicationService.GetApplyScreenAsync` | 100% | 100% |
| `ApplicationStatusWorkflow` | 100% | 83% |
| `ApplicationRepository` | 81% | 100% |
| `ApplicationStatusValueConverter` | 96% | 70% |
| `NotificationEventService.PublishNewApplicationReceivedAsync` | 95% | 100% |

> The defect-relevant paths are at/near full coverage. Whole-solution line-rate is ~25% because large
> unrelated services (Copilot, Candidate import, analytics) are out of scope for this iteration — this
> is reported honestly rather than claimed as 100%.

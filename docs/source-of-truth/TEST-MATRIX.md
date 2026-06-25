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
| T-CTX-001 | `GetApplyScreenAsync_AfterWithdrawal_ReportsCanApplyAndNotAlreadyApplied` | unit | BR-001/002 | canApply=true, alreadyApplied=false |
| T-WF-001 | `ApplicationStatusWorkflowTests.IsClosed_DetectsTerminalStates` (incl. Withdrawn) | unit | DL-002 | Withdrawn is closed |
| T-WF-002 | `ApplicationStatusWorkflowTests.CanCandidateWithdraw_MatchesPolicy` (incl. Withdrawn/Rejected) | unit | BR-003 | cannot withdraw closed |
| T-CV-001 | `ApplicationStatusValueConverter_MapsLegacyStrings` (incl. `withdrawn`/`Withdrawn`) | unit | DL-002 | round-trips Withdrawn |
| T-E2E-001 | `ApplicationService_WithdrawThenReapply_SucceedsWithoutConflictOr500` | integration (PG) | BUG-001/002 | withdraw 200 → reapply 201 → duplicate 409 |
| T-API-001 | `ApplicationController_ApplyContext_Should_Require_Candidate…` | integration (PG) | authz | candidate 200, HR-as-candidate 403 |
| T-API-002 | `Public_Apis_Should_Return_Controlled_404_Not_500_For_Invalid_Ids` | integration (PG) | 500 eradication | 404 not 500 |

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

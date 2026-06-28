# Notification Routing Final Report

## 1. Branch
- Current branch: `feat/notification-routing-deeplinks` (created from `develop`; not a direct commit to develop)
- No PR created (the user will create the PR manually).

## 2. Scope completed
- Backend notification routing: ownership-based recipients, recipient deduplication, all workflow events.
- Deep link URL support: every notification carries a role-aware frontend `url` + `targetType` + `targetId`
  (and `secondaryTargetId`/`routeHint` where relevant), stored in `notifications.data_json` and mirrored
  onto `notifications.entity_type`/`entity_id`.
- Tests: 24 new backend unit tests (`RecruitPro.Tests/NotificationRoutingTests.cs`, T-NOTI-001…023);
  existing notification/test constructions updated for the new constructors and renamed method.
- Docs: source-of-truth docs updated (see §11).

## 3. Events implemented
- `job_submitted_for_approval`
- `job_approved`
- `job_rejected`
- `application_applied` (migrated from legacy `new_application_received`)
- `application_screening_started`
- `application_department_head_review_requested`
- `application_interview_requested`
- `application_withdrawn`
- `interview_scheduled`
- `interview_completed`
- `offer_email_sent`
- `rejection_email_sent`
- `offer_accepted`
- `offer_declined`
- `candidate_hired` — **defined but intentionally not emitted** (folded into `offer_accepted`, Option A)

## 4. Events deferred
- **`candidate_hired`** — Reason: accepting an offer hires the candidate in the same action
  (`AcceptOfferAsync` sets `Hired` + `OfferStatus.Accepted` together). To avoid duplicate noise we emit
  `offer_accepted` only (Option A). The event code exists for future use.
- **`application_screening_started`** is implemented (candidate-facing) and emitted on `Applied → Screening`.
- **Candidate in-app delivery surfacing** (the bell/dropdown UI) — deferred to a frontend phase. Candidate
  notification *records* are created and click-ready.

## 5. Recipient routing
- Candidate: `Application.UserId`.
- Recruiter: `Application.AssignedRecruiterId ?? Job.RecruiterId ?? Job.CreatedBy`.
- DepartmentHead: `Application.AssignedDepartmentHeadId ?? Job.Department.HeadUserId ?? Job.ApprovedBy`.
- SystemAdmin: not a default recruitment recipient (BR-OWN-009); not routed to.
- Deduplication behavior: recipients are deduplicated by user id (first role-appropriate link wins);
  null/`Guid.Empty` users and unresolved users are skipped. A user filling multiple roles gets one row.

## 6. Deep link routing
- url: frontend route (never an API route). Examples: `/hr/applications/{id}`,
  `/manager/applications/{id}`, `/manager/jobs/{id}/approval`, `/hr/interviews/schedule?applicationId={id}`,
  `/candidate/my-applications?applicationId={id}`, `/candidate/interviews?interviewId={id}`, `/jobs/{id}`.
- targetType: `job | job_approval | application | application_review | interview | interview_request | offer`.
- targetId: the primary entity id for the target (job/application/interview/offer); `secondaryTargetId`
  carries the related id (e.g. applicationId alongside an interviewId).
- Role-aware routing: links differ per recipient role for the same event.
- Candidate-safe URLs: `/candidate/...` only (asserted not to contain `/hr/` or `/manager/`).
- HR-safe URLs: `/hr/...`.
- DepartmentHead-safe URLs: `/manager/...`.
- Documented fallback: HR/recruiter job detail → public `/jobs/{id}` (no internal HR job-detail route);
  candidate application/offer/interview detail → the candidate list routes (no detail-by-id routes yet).

## 7. Best-effort behavior
- Business actions do not fail if notification publishing fails: each publish runs **after** the DB commit
  and is wrapped in `try/catch` at the call site (ApplyAsync, WithdrawApplicationAsync,
  UpdateApplicationDecisionAsync, SendRejectionEmailAsync, AcceptOfferAsync, DeclineOfferAsync,
  CreateInterviewAsync, UpdateInterviewStatusAsync, CreateJobAsync, PatchJobAsync, SendOffer).
- Logging behavior: failures are logged via `ILogger<T>.LogError` with the entity id and the failed event;
  no rollback, no 500. Verified by T-NOTI-016 and the existing apply 500-eradication regression test.

## 8. Payload fields
- eventCode, url, targetType, targetId, secondaryTargetId, routeHint?
- applicationId, jobId, jobTitle, candidateUserId, candidateName, actorUserId
- oldStatus/newStatus, departmentId, recruiterId, departmentHeadId
- interviewId, offerId, scheduledAt, departmentHeadReviewRequestedAt, reason?

## 9. Legacy compatibility
- Existing event compatibility: `application_status_changed` and `candidate_score_ready` preserved;
  `interview_scheduled` keeps its code (now ownership-routed); apply event migrated
  `new_application_received` → `application_applied` (publisher method renamed
  `PublishNewApplicationReceivedAsync` → `PublishApplicationAppliedAsync`).
- Old records preserved: no notification rows or seed data were deleted; no DB migration was required
  (the existing `data_json` jsonb column stores the deep-link metadata).

## 10. Tests added/updated
- New file: `RecruitPro.Tests/NotificationRoutingTests.cs` — 24 tests (T-NOTI-001…023, plus a no-head
  guard case).
- Updated constructors / mocks: `ApplicationLayerServiceUnitTests.cs` (JobService/OfferService/
  InterviewService now take `INotificationEventService` + `ILogger<T>`; removed two now-obsolete
  role-broadcast tests), `OwnershipGuardUnitTests.cs`, `ApplicationConformanceTests.cs`,
  `WorkflowDecisionEmailTests.cs`, `ApplicationReapplyRegressionTests.cs` (renamed publisher mock).
- Result: **235 passed / 0 failed** (full suite incl. Testcontainers integration tests).

## 11. Docs updated
- `docs/source-of-truth/NOTIFICATION-EVENT-MATRIX.md` (rewritten to implemented state)
- `docs/source-of-truth/BUSINESS-RULES.md` (BR-NOTI-001…006)
- `docs/source-of-truth/STATE-MACHINE.md` (events per transition)
- `docs/source-of-truth/API-CONTRACT.md` (notification payload/deep-link contract)
- `docs/source-of-truth/ERROR-CONTRACT.md` (no new error codes; best-effort)
- `docs/source-of-truth/TEST-MATRIX.md` (T-NOTI-001…023)
- `docs/source-of-truth/FE-BE-WORKFLOW-CONTRACT-AUDIT.md` (FE inspected only; bell deferred)
- `docs/source-of-truth/IMPLEMENTATION-PLAN-OWNERSHIP.md` (Phase 6 marked done)

## 12. Commands run
- `dotnet build RecruitProInternal.sln`
- `dotnet test RecruitProInternal.sln` (full suite, includes Testcontainers integration tests)
- Frontend checks: none run — no frontend production files were touched (routes inspected read-only).

## 13. Build/test result
- Backend: build succeeded (0 errors); tests **235 passed, 0 failed, 0 skipped**.
- Frontend: not touched (no build required).

## 14. Remaining risks
- Risk: HR/recruiter job notifications deep-link to the public `/jobs/{id}` page (no internal HR job-detail
  route exists yet). Follow-up: add an internal HR job-detail route and repoint `NotificationLinks.HrJob`.
- Risk: candidate links target list routes (no detail-by-id route). They include `?applicationId=`/
  `?interviewId=` query hints the list can honor later. Follow-up: add candidate detail routes.
- Risk: `job_rejected` carries no rejection reason (no reason field is stored on the job/patch request;
  none was invented). Follow-up: add reason storage if product wants it surfaced.
- Follow-up: build the frontend notification bell/dropdown to consume the click-ready records.

## 15. Confirmations
- No status/role rename (`ManagerReview`, `Manager`, `HeadDepartment` unchanged; UI label "Head Review").
- No `Job.HiringManagerId` added.
- No PR created.

## 16. Follow-up fix: realtime, seen/read, click navigation, schedule date

- **Realtime:** SignalR hub already registered at `/hubs/notifications`. `NotificationProvider.tsx` connects via `HubConnectionBuilder` with access token and bounded-backoff retry. Frontend listens to `notification:new` and prepends to bell list, incrementing `unseenCount` / `unreadCount` without page refresh. Delivery is per-user via `IHubContext.Clients.User(userId)`.
- **Seen/read:** Added `is_seen` / `seen_at` / `read_at` columns to `notifications` table. Backend: new `POST /api/notifications/seen` marks all as SEEN (not read); `POST /api/notifications/{id}/read` marks one as READ (also sets seen). `GET /api/notifications/counts` returns `{ unseen, unread }`. Bell badge uses `unseen`. Opening bell calls `markAllSeen` only. Clicking an item calls `markAsRead` (marks READ). Unread item styling persists until item click.
- **Click navigation:** `AppHeader.tsx` calls `resolveDeepLinkUrl(notification)` to extract `notification.data.url`. Uses `navigate(url)` (React Router). Falls back to toast if URL is missing or malformed. Bell never crashes on bad `data_json`.
- **Schedule date bug:** Root cause — `DateOnly.ToDateTime(TimeOnly.MinValue).AddMinutes(...)` produced `DateTime.Kind = Unspecified` which, combined with runtime/Npgsql behavior, could shift the day. Fix: `DateTime.SpecifyKind(new DateTime(year, month, day, h, m, 0), DateTimeKind.Unspecified)` builds date from explicit components. Frontend `JobInterviewListScreen.tsx` custom-range init changed from `toISOString().slice(0,10)` (UTC) to local-date components to prevent the range display showing yesterday's date in early VN morning.
- **Tests:** `NotificationSeenReadTests.cs` — T-NOTI-FE-001..006, T-INTERVIEW-DATE-001 + 001b (unit, no DB needed).
- **Commands:**
  ```
  dotnet build RecruitProInternal.sln
  dotnet test RecruitProInternal.sln
  npx tsc --noEmit   (in recruit-pro-internal)
  npx vite build     (in recruit-pro-internal)
  ```
- **Result:** Build passes, all tests pass, no new TS errors introduced.

> **NOTE (superseded by §17):** The SignalR realtime described in §16 was replaced by SSE. The
> seen/read, click-navigation and schedule-date work in §16 remains in force.

## 17. Follow-up fix: SSE realtime, seen/read, click navigation, schedule date

- **SignalR issue:** In production the SignalR hub negotiate failed —
  `POST https://www.recruitpro.site/hubs/notifications/negotiate?negotiateVersion=1` → **405 Not Allowed**.
  The reverse proxy did not forward the hub's negotiate/WebSocket upgrade path, so realtime delivery was
  permanently dead. Notifications only need **one-way** server→client realtime, for which SignalR's
  negotiate/WebSocket/transport-fallback machinery is unnecessary complexity.
- **SSE endpoint:** `GET /api/notifications/stream` — authenticated, user-scoped, `text/event-stream`.
  Emits `event: notification.created` frames (same DTO shape as `GET /api/notifications`) plus `: ping`
  heartbeat comments every 25s. Sends `Cache-Control: no-cache`, `Connection: keep-alive`,
  `X-Accel-Buffering: no`, disables response buffering, flushes after every write, and stops cleanly on
  request cancellation (client disconnect).
- **Broker:** `INotificationSseBroker` + `InMemoryNotificationSseBroker` (singleton). Per-user, per-tab
  **bounded** channels (capacity 64, `DropOldest`); `PublishAsync(userId, dto)` fans out only to that
  user's tabs — never a broadcast. Disposing a subscription removes the tab; the publisher never blocks
  on delivery. The DB row remains the source of truth (best-effort realtime).
- **Auth strategy:** **Option A — fetch-based SSE.** The app stores a JWT in `localStorage` and uses
  `Authorization: Bearer`. Native `EventSource` cannot set headers, so the frontend opens the stream with
  `fetch` (`Authorization: Bearer <token>`, `Accept: text/event-stream`) and parses the stream manually
  (`src/services/notification/notificationStream.ts`). The token stays in a header — never in the URL/query,
  never logged. The dead `/hubs/notifications` query-token branch was removed from `JwtExtension.cs`.
- **Nginx/deploy note:** SSE requires proxy buffering disabled. Add a dedicated location (see
  `docs/source-of-truth/API-CONTRACT.md`):
  ```nginx
  location /api/notifications/stream {
      proxy_pass http://127.0.0.1:5000/api/notifications/stream;
      proxy_http_version 1.1;
      proxy_set_header Host $host;
      proxy_set_header X-Real-IP $remote_addr;
      proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
      proxy_set_header X-Forwarded-Proto $scheme;
      proxy_buffering off;
      proxy_cache off;
      proxy_read_timeout 3600s;
      proxy_send_timeout 3600s;
      add_header X-Accel-Buffering no;
  }
  ```
  If a broad `/api/` location proxies the app, ensure SSE inherits `proxy_buffering off`,
  `proxy_read_timeout 3600s`, and `X-Accel-Buffering no`. No SignalR `/hubs/` location is needed anymore.
- **Seen/read:** Unchanged from §16. Bell badge = `unseen`. Opening the bell calls `POST /api/notifications/seen`
  (mark all SEEN only, never READ). Clicking an item calls `POST/PATCH /api/notifications/{id}/read` (READ +
  seen) and navigates. A pushed SSE event arrives `isSeen:false, isRead:false` and increments both counts.
- **Click navigation:** Unchanged from §16. `AppHeader.resolveDeepLinkUrl` extracts `notification.data.url`
  (parses string-JSON safely), `navigate(url)` via React Router; missing/malformed url → toast
  "Không tìm thấy đường dẫn thông báo.", no crash, no navigation. Read still attempted before navigation;
  navigation proceeds even if mark-read fails (when a url exists).
- **Schedule date bug:** Confirmed fixed and hardened. `CreateInterviewRequest.Date` is a `DateOnly`, so the
  JSON `"2026-06-28"` binds straight to day 28 with no timezone conversion possible; the backend builds the
  timestamp with `DateTime.SpecifyKind(new DateTime(y,m,d,h,m,0), DateTimeKind.Unspecified)`. The frontend
  schedule screen sends a **local** `YYYY-MM-DD` (built from `getFullYear/Month/Date`, never `toISOString()`).
- **Tests:**
  - Backend: `NotificationSseTests.cs` — T-SSE-002 (target-only), T-SSE-003 (multi-tab) + 003b (disposed),
    T-SSE-004 (publish after DB save), T-SSE-005 (publish failure does not fail `CreateInterview`).
    `ApiIntegrationTests.cs` — T-SSE-001 (`/api/notifications/stream` returns 401 without token).
    Existing `NotificationSeenReadTests.cs` (T-NOTI-FE-001..006, T-INTERVIEW-DATE-001/001b) retained.
  - Frontend E2E: `notification-sse-bell.e2e.ts` — E2E-NOTI-001 (SSE event appears w/o refresh),
    002/003 (bell opens → seen only), 004/005 (item click → read + navigate), 006 (malformed url → toast).
    `interview-schedule-date.e2e.ts` — E2E-INTERVIEW-001 (day 28 stays 28 in the create payload).
- **Commands:**
  ```
  dotnet build RecruitProInternal.sln
  dotnet test RecruitProInternal.sln
  npx tsc --noEmit          # in recruit-pro-internal (pre-existing CommonSelect.tsx baseline error only)
  npx vite build            # in recruit-pro-internal
  npx eslint .              # in recruit-pro-internal
  npx playwright test       # in recruit-pro-internal
  ```
- **Result:** Backend builds (0 errors); SSE/seen-read/date unit tests pass (16/16). Frontend `vite build`
  succeeds, eslint clean, all 23 Playwright e2e tests pass. `@microsoft/signalr` dependency removed.

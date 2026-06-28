# Notification Event Matrix

**Status:** **Backend notification routing is IMPLEMENTED (Phase 6). Frontend realtime, seen/read semantics, and click navigation are IMPLEMENTED (Phase 7 follow-up fix).**

Recipients are resolved from the ownership snapshot (`IApplicationOwnershipResolver` / `Job` ownership fields), deduplicated, and every notification carries a **role-aware, click-ready frontend deep link** (`url` / `targetType` / `targetId`). Publishing is **best-effort and post-commit**: a publishing failure never fails or rolls back the business action.

### Seen vs Read semantics (MUST NOT confuse)

| Term | Trigger | DB columns set | What it drives |
|---|---|---|---|
| **SEEN** | User opens the notification bell/dropdown | `is_seen = true`, `seen_at = now` | Bell badge count (unseen) |
| **READ** | User clicks a specific notification item | `is_read = true`, `read_at = now`, `is_seen = true` | Unread item styling |

- Bell badge displays **unseen** count (`GET /api/notifications/counts → data.unseen`).
- Opening the bell calls `POST /api/notifications/seen` (marks all seen, NOT read).
- Clicking a notification item calls `POST /api/notifications/{id}/read` then navigates to `notification.data.url`.
- `data.url` is the role-aware deep link built by `NotificationLinks.cs`.

### Realtime delivery

SignalR hub at `/hubs/notifications`. Event name: `notification:new`. Delivery is per-user via `IHubContext.Clients.User(userId)`. Frontend `NotificationProvider` listens and prepends incoming notifications to the bell list and increments `unseenCount` / `unreadCount` without page refresh.

`ManagerReview` remains the canonical application status (NOT renamed); the UI display label for that
stage is **Head Review**. The `Manager` / `HeadDepartment` roles are NOT renamed. No `Job.HiringManagerId`
was added.

Recipient roles use business names: **Candidate**, **HR / Recruiter**, **DepartmentHead**.

Implementation:
- Event codes: `RecruitPro.Domain/Constants/NotificationEventCodes.cs`
- Target types: `RecruitPro.Domain/Constants/NotificationTargetTypes.cs`
- Deep-link URL builder (role-aware): `RecruitPro.Application/Notifications/NotificationLinks.cs`
- Message templates (VI): `RecruitPro.Application/Notifications/NotificationTemplates.cs`
- Publisher: `RecruitPro.Application/Services/NotificationEventService.cs`
- Payload is stored in `notifications.data_json`; `targetType`/`targetId` are mirrored onto
  `notifications.entity_type` / `entity_id`.

---

## 1. Ownership resolver (recipient routing)

Routing uses ownership fields, **not** broad role broadcast:

```
Candidate      = Application.UserId
Recruiter      = Application.AssignedRecruiterId ?? Job.RecruiterId ?? Job.CreatedBy
DepartmentHead = Application.AssignedDepartmentHeadId ?? Job.Department.HeadUserId ?? Job.ApprovedBy
```

Rules: deduplicate all recipients; never notify a null user; never notify *all* `Manager` /
`HeadDepartment` users when a specific assigned DepartmentHead exists.

---

## 2. Implemented event matrix

| Event code | Trigger | Recipients | Deep link (per role) | targetType | targetId |
|---|---|---|---|---|---|
| `job_submitted_for_approval` | Job created → PendingApproval | DepartmentHead (`Department.HeadUserId`; none ⇒ no notification) | `/manager/jobs/{jobId}/approval` | `job_approval` | jobId |
| `job_approved` | Job → Approved | Recruiter (`Job.RecruiterId ?? CreatedBy`) | `/jobs/{jobId}` *(fallback, see §4)* | `job` | jobId |
| `job_rejected` | Job → Rejected | Recruiter | `/jobs/{jobId}` *(fallback)* | `job` | jobId |
| `application_applied` | Candidate applies | Recruiter **only** (HR-first, BR-OWN-006) | `/hr/applications/{applicationId}` | `application` | applicationId |
| `application_screening_started` | Applied → Screening | Candidate | `/candidate/my-applications?applicationId={id}` | `application` | applicationId |
| `application_department_head_review_requested` | Screening → ManagerReview | DepartmentHead | `/manager/applications/{applicationId}` | `application_review` | applicationId |
| `application_interview_requested` | ManagerReview → Interview | Recruiter (to schedule) | `/hr/interviews/schedule?applicationId={id}` | `interview_request` | applicationId |
| `application_withdrawn` | Candidate withdraws | Recruiter; + DepartmentHead **only if** prior status ∈ {ManagerReview, Interview, Offer} | Recruiter `/hr/applications/{id}`; Head `/manager/applications/{id}` | `application` / `application_review` | applicationId |
| `interview_scheduled` | Interview created | Candidate + Recruiter + DepartmentHead + Interviewer (deduped) | Candidate `/candidate/interviews?interviewId={id}`; Recruiter/Interviewer `/hr/interviews?interviewId={id}`; Head `/manager/applications/{appId}` | `interview` | interviewId (secondary=applicationId) |
| `interview_completed` | Interview → Completed | Recruiter + DepartmentHead (no candidate) | Recruiter `/hr/applications/{id}`; Head `/manager/applications/{id}` | `application` / `application_review` | applicationId (secondary=interviewId) |
| `offer_email_sent` | Offer email sent & app → Offer | Candidate + Recruiter + DepartmentHead | Candidate `/candidate/my-applications?applicationId={id}`; internal `/hr/applications/{id}` & `/manager/applications/{id}` | `offer` | offerId (secondary=applicationId) |
| `rejection_email_sent` | Rejection email sent & app → Rejected | Candidate + Recruiter + DepartmentHead | Candidate `/candidate/my-applications?applicationId={id}`; Recruiter `/hr/applications/{id}`; Head `/manager/applications/{id}` | `application` / `application_review` | applicationId |
| `offer_accepted` | Candidate accepts offer | Recruiter + DepartmentHead | `/hr/applications/{id}` & `/manager/applications/{id}` | `offer` | offerId (secondary=applicationId) |
| `offer_declined` | Candidate declines offer | Recruiter + DepartmentHead | `/hr/applications/{id}` & `/manager/applications/{id}` | `offer` | offerId (secondary=applicationId) |

### candidate_hired decision

**Option A (chosen):** accepting an offer hires the candidate in the **same** action
(`AcceptOfferAsync` sets `Hired` + `OfferStatus.Accepted` together), so we emit **`offer_accepted` only**
and do **not** also emit `candidate_hired` — avoiding duplicate noise. The `candidate_hired` event code
exists in `NotificationEventCodes` but is intentionally not published.

---

## 3. Routing principles (enforced in code)

- **HR-first on apply (BR-OWN-006):** `application_applied` → recruiter only, never the DepartmentHead.
- **Ownership snapshot (BR-OWN-005):** application-scoped events resolve recipients from the snapshotted
  `AssignedRecruiterId` / `AssignedDepartmentHeadId` with the audit/role fallbacks above.
- **Best-effort, post-commit (BR-APPLICATION-005/010):** every publish runs AFTER the DB commit, wrapped
  in `try/catch` at each call site; a failure is logged and never rolls back / 500s the business action.
- **Role-aware links:** a candidate is never routed to an HR/Manager screen; HR/Head are never routed to
  candidate-only screens.
- **Email is not replaced:** `offer_email_sent` / `rejection_email_sent` fire only *after* the candidate
  email send succeeds (the email remains the primary channel; the in-app record is additive).
- **SystemAdmin is not a recruitment recipient (BR-OWN-009).**

---

## 4. Deep-link route inventory & documented fallbacks

Verified against `recruit-pro-internal/src/routes`. Where no dedicated route exists yet, the nearest
stable route is used and documented:

| Need | Route used | Note |
|---|---|---|
| Candidate application / offer detail | `/candidate/my-applications?applicationId={id}` | No detail-by-id route; list is the candidate-safe target. |
| Candidate interview detail | `/candidate/interviews?interviewId={id}` | No detail-by-id route; list is the candidate-safe target. |
| HR application / candidate review detail | `/hr/applications/{applicationId}` | Dedicated route. |
| HR interview scheduling | `/hr/interviews/schedule?applicationId={id}` | Dedicated route. |
| HR/Recruiter job detail | `/jobs/{jobId}` | **Fallback** — no internal HR job-detail route; public job page used. |
| DepartmentHead candidate review | `/manager/applications/{applicationId}` | Dedicated route. |
| DepartmentHead job approval | `/manager/jobs/{jobId}/approval` | Dedicated route. |

URLs are **frontend routes, not API routes** (asserted in tests: `url` never starts with `/api/`).

---

## 5. Payload

`notifications.data_json` contains the event payload. Common keys (only relevant ones included per
event): `eventCode`, `url`, `targetType`, `targetId`, `secondaryTargetId`, `routeHint?`, `applicationId`,
`jobId`, `jobTitle`, `candidateUserId`, `candidateName`, `actorUserId`, `oldStatus`, `newStatus`,
`departmentId`, `recruiterId`, `departmentHeadId`, `interviewId`, `offerId`, `scheduledAt`,
`departmentHeadReviewRequestedAt`, `reason?`.

**No orphan notifications:** every notification is clickable to a role-appropriate detail screen.

---

## 6. Legacy compatibility

- Old rows are **never deleted**; legacy event codes are preserved.
- The apply event was migrated from `new_application_received` (fan-out to `Job.CreatedBy` + all `HR`) to
  `application_applied` (assigned-recruiter-only). The publisher method was renamed
  `PublishNewApplicationReceivedAsync` → `PublishApplicationAppliedAsync`.
- `application_status_changed` (candidate status updates) and `candidate_score_ready` are preserved; the
  decision endpoint now routes the specific ownership events above for the Screening→ManagerReview,
  ManagerReview→Interview and Applied→Screening transitions, falling back to `application_status_changed`
  for any other transition.
- `interview_scheduled` keeps its event code but now routes by ownership (was `HeadDepartment` role
  broadcast).

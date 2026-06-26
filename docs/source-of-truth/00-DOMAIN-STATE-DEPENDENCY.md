# 00 — Domain State Dependency (READ FIRST)

This is the **first document to read** in the source-of-truth set. Every business rule, state
machine, API contract, and test must trace back to the dependency model defined here. If a rule or a
piece of code cannot be traced to a dependency below, it is either undocumented or wrong.

The core principle: **`Application.status` is the central recruitment-pipeline state, but it is never
interpreted in isolation.** Most defects in this system come from reading one table — "does an
application row exist? → already applied" — instead of reading the *dependency*: "does an _active_
application exist for this candidate + job, given the job is still open and the profile is ready?"

---

## 1. Source of truth per business decision

For each decision the system makes, exactly one table is the source of truth. Everything else is a
precondition, a dependent record, or a derived/side-effect view.

| Business decision | Source of truth | Reads (preconditions) |
|---|---|---|
| Can this job receive new applications? | `Job.status` + `Job.deadline` | — |
| Is the candidate ready to apply? | derived from `CandidateProfile` + `Resume` | `User` contact info |
| Does the candidate already have a live application here? | `Application.status` (active set) | `Application.UserId`, `Application.JobId` |
| Where is a candidate in the pipeline? | `Application.status` | Job/Profile/Resume at creation time |
| Is an interview valid / actionable? | `Interview.status` **gated by** `Application.status` | `Application` reached `Interview` |
| Is an offer valid / actionable? | `Offer.status` **gated by** `Application.status` | `Application` reached `Offer` |
| Should a user be notified? | committed business event | the committed row that changed |
| Dashboard / analytics numbers | derived from canonical state groups | `Job` + `Application` + `Interview` + `Offer` |

---

## 2. State classification

### 2.1 Persisted workflow states

These are stored in the database and drive the workflow. (Enums verified against
`RecruitPro.Domain.Enums`.)

| Table | Enum | Values (as in code) |
|---|---|---|
| `Job` | `JobStatus` | `Draft`, `PendingApproval`, `Approved`, `Closed`, `Rejected` |
| `Application` | `ApplicationStatus` | `Applied`, `Screening`, `ManagerReview`, `Interview`, `Offer`, `Hired`, `Rejected`, `OfferDeclined`, `Withdrawn` |
| `Interview` | `InterviewStatus` | `Scheduled`, `Completed`, `Canceled` |
| `Offer` | `OfferStatus` | `Draft`, `Sent`, `Accepted`, `Declined` |
| `Notification` | (read flag) | `Unread` / `Read` |

> **Reality note — do not document states that don't exist.** `JobStatus` has **no** `Archived` or
> `Deleted` value (it has `Rejected`). `OfferStatus` has **no** `Cancelled` value. `InterviewStatus`
> spells the cancel state `Canceled` (one `l`). Earlier drafts of these docs referenced
> `Archived/Deleted` and `Offer.Cancelled`; those are aspirational and are flagged in
> [DECISION-LOG.md](DECISION-LOG.md) as enum gaps, not current behavior.

### 2.2 Derived states (never stored separately)

| Derived value | Computed from | Rule |
|---|---|---|
| `Application.Active` | `Application.status` | `!IsClosed(status)` |
| `Application.Closed` | `Application.status` | `IsClosed(status)` — `Hired`/`Rejected`/`OfferDeclined`/`Withdrawn` |
| `AlreadyApplied` | existence of an **active** application for (candidate, job) | **EXISTS active**, not "latest row is active" (see §6) |
| `CanApply` | job eligibility **and** profile/resume readiness **and** no active application | all preconditions pass |
| Candidate readiness | `CandidateProfile` completeness + current `Resume` | both required |
| Reviewer allowed transitions | `Application.status` | `ApplicationStatusWorkflow.AllowedTransitions` |
| Candidate visible actions | `Application.status` + ownership + offer/interview availability | derived per request |

### 2.3 Side-effect-only state

| Record | Driven by | Must never drive |
|---|---|---|
| `Notification` | a committed business event | the business state itself |
| Semantic / rule scoring | a committed `Application` | apply success/failure |
| Dashboard / analytics | canonical state groups | any write decision |

---

## 3. Canonical state groups (the vocabulary all other docs use)

```
ActiveApplicationStates      = { Applied, Screening, ManagerReview, Interview, Offer }
ClosedForWorkflow            = { Rejected, Withdrawn, OfferDeclined, Hired }
ReapplyEligibleClosedStates  = { Rejected, Withdrawn, OfferDeclined }
```

- **Active** = candidate is still inside the recruitment pipeline.
- **ClosedForWorkflow** = the row is history; it must never be mutated back into an active state, and
  it is excluded from active-pipeline analytics.
- **ReapplyEligibleClosedStates** = closed states from which the candidate may start a **new**
  application for the same job.

**`Hired` is closed-for-workflow and counts in analytics, but is NOT re-apply-eligible for the same
`jobId`.** A candidate already hired for a posting must not re-apply to that same posting; a new hiring
need is a new job posting. (There is no "new posting cycle" concept in the schema today, so `Hired` is
terminal for that `jobId`.) This supersedes the earlier "Hired → re-apply allowed" wording — see
[DECISION-LOG.md](DECISION-LOG.md) DL-001/DL-007.

> Mapping to code today: `ApplicationStatusWorkflow.IsClosed` already returns these four
> `ClosedForWorkflow` states. There is **no** distinct `ReapplyEligibleClosedStates` predicate yet —
> re-apply currently keys only off "no active application", which means it would also permit re-apply
> after `Hired`. Closing that gap is tracked in [DECISION-LOG.md](DECISION-LOG.md) DL-007.

---

## 4. Dependency map (authoritative)

```
User / Role
  ↓ determines permissions
CandidateProfile (completeness)
  ↓ required for apply readiness
Resume (current resume)
  ↓ required for apply readiness
Job.status + Job.deadline
  ↓ controls whether an Application can be created at all
Application.status        ← CENTRAL PIPELINE STATE
  ↓ gates                  ↓ gates                ↓ drives
Interview.status        Offer.status           Notification (side effect)
  (only after            (only after             ↓
   Application=Interview)  Application=Offer)    Dashboard / Analytics (derived)
```

Read the arrows as **"controls / gates / drives"**, never the reverse. A downstream record can never
push an upstream record into a new state: a `Notification` cannot change an `Application`; an `Offer`
existing does not by itself make the `Application` `Hired`.

---

## 5. Dependency table (per state)

| Table | State | Depends on | Drives / gates |
|---|---|---|---|
| Job | Draft / PendingApproval / Approved / Closed / Rejected | HR/Manager/Admin actions | apply eligibility, public job list |
| CandidateProfile | Complete / Incomplete (derived) | User data | apply eligibility |
| Resume | HasCurrentResume (derived) | uploaded CV | apply eligibility, scoring |
| Application | Applied / Screening / ManagerReview / Interview / Offer / Hired / Rejected / OfferDeclined / Withdrawn | Job + Profile + Resume + existing active app | Interview, Offer, Notification, Dashboard |
| Interview | Scheduled / Completed / Canceled | `Application = Interview` (reached or in) | `Application → Offer / Rejected` |
| Offer | Draft / Sent / Accepted / Declined | `Application = Offer` | `Application → Hired / OfferDeclined` |
| Notification | Unread / Read | a committed business event | FE notification UI only |
| Dashboard | derived metrics | Application / Job / Interview / Offer states | reporting only |

### 5.1 Application status → required previous facts

| Status | Required previous facts | Gates / unlocks |
|---|---|---|
| `Applied` | Approved non-expired job + complete profile + current resume + **no active duplicate** | HR may screen/reject; candidate may withdraw |
| `Screening` | committed `Applied` accepted by HR | HR may move to ManagerReview/Rejected; candidate may withdraw |
| `ManagerReview` | HR screening passed | Manager may move to Interview/Rejected; candidate may withdraw |
| `Interview` | manager review passed | interview scheduling becomes valid; candidate may withdraw |
| `Offer` | interview stage passed and reviewer ready | a `Sent` offer; candidate accept/decline |
| `Hired` | an `Offer` exists and candidate **accepted** it | hiring completion/reporting; terminal for jobId |
| `Rejected` | reviewer closed it from an allowed active state | history; re-apply eligible |
| `OfferDeclined` | an `Offer` exists and candidate **declined** it | history; re-apply eligible |
| `Withdrawn` | candidate owns an active app and withdrew before Offer | history; re-apply eligible; pending interview → stale |

---

## 6. Duplicate-apply rule depends on ACTIVE application, not any application

The single most important rule in the system:

> A candidate is blocked from applying **only if an ACTIVE application exists** for the same job.

Correct check (set semantics):

```
AlreadyApplied = EXISTS application WHERE
    application.UserId == currentUserId
AND application.JobId == jobId
AND application.Status ∈ ActiveApplicationStates
```

**Not** `existingApplication != null`, and **not** "the most recent application is active." Production
data can be dirty or racy (two rows for one job), and "latest row" can pick a closed row while an
active one exists, or vice versa.

> Code reality (implemented): the duplicate decision uses
> `IApplicationRepository.HasActiveApplicationAsync(userId, jobId)` — a SQL `EXISTS` over the active
> status set, independent of row ordering (DL-008). Its defense-in-depth partner is the DB-level
> partial unique index `ux_applications_active_user_job` in `init.sql` + `OnModelCreating`
> (INV-014 / DL-011). Verified by T-DUP-003 and T-DUP-004.

---

## 7. Cross-state invalidation rules (what must change downstream)

When an upstream state changes, dependent records must be created, cancelled, or treated as stale.

| Upstream change | Required downstream effect |
|---|---|
| `Application → Interview` | an `Interview` may be created/scheduled |
| `Application → Offer` | an `Offer` should exist/be `Sent` before candidate response |
| `Application → Withdrawn` (from Interview) | pending `Interview` (`Scheduled`) must be `Canceled` / treated as stale; no offer path remains |
| `Application → Rejected` (with pending interview/offer) | pending `Interview` must be `Canceled`; no active offer should continue |
| `Offer Accepted` | `Application → Hired` (and only via candidate accept) |
| `Offer Declined` | `Application → OfferDeclined` |
| any committed status change | `Notification` published **after** commit, best-effort |

> Code reality: today the withdraw/reject paths set the `Application` status but do **not** yet
> proactively cancel a pending `Interview` — the FE is expected to treat it as stale. Promoting this to
> an enforced server-side cascade is tracked in [DECISION-LOG.md](DECISION-LOG.md) DL-009.

---

## 8. Invariants (the contract these docs enforce)

| ID | Invariant |
|---|---|
| INV-001 | Only `Approved`, non-expired Jobs can receive new Applications. |
| INV-002 | Candidate must have a complete profile and a current resume before applying. |
| INV-003 | At most **one ACTIVE** Application per (candidate, job); enforced by **EXISTS active**, not "latest row". |
| INV-004 | `Withdrawn` is not `Rejected`. |
| INV-005 | `Rejected` is a company decision; `Withdrawn` is a candidate decision. |
| INV-006 | Closed application rows are history and must not be mutated back into an active state. |
| INV-007 | Re-apply creates a **new** Application row; it never reactivates an old row. |
| INV-008 | `Application.status` drives Interview/Offer availability — never the reverse. |
| INV-009 | Offer accept/decline are candidate-owned actions; `Hired` only comes from candidate accept. |
| INV-010 | A notification failure must never roll back a committed business action. |
| INV-011 | Dashboard and analytics must derive from canonical state groups. |
| INV-012 | Frontend must not infer business state from localized labels. |
| INV-013 | Business errors return stable 4xx, never 500. |
| INV-014 | Duplicate-active-application must be enforced at the **database** level, not only the service. |
| INV-015 | `Hired` is terminal for its `jobId`: closed-for-workflow, counted in analytics, **not** re-apply-eligible. |

INV-001…INV-015 are **enforced in code and verified by tests** as of the backend conformance pass —
see [CONFORMANCE-AUDIT.md](CONFORMANCE-AUDIT.md) and [DECISION-LOG.md](DECISION-LOG.md)
DL-007/008/009/011. INV-014's DB index lives in `init.sql` + `OnModelCreating` (not an EF migration).

---

## 9. End-to-end reference flow

```
Job Draft → PendingApproval → Approved
  ↓ (candidate opens job)
BE builds apply context:
  job Approved? · deadline valid? · profile complete? · current resume? · no ACTIVE application?
  ↓ all pass → canApply = true
Candidate applies → Application = Applied → commit
  ↓ AFTER commit (best-effort): notify HR, enqueue scoring
HR: Applied → Screening → ManagerReview
Manager: ManagerReview → Interview → (schedule Interview)
HR/Manager: Interview → Offer → (Offer Sent)
Candidate: accept → Offer Accepted + Application Hired
           decline → Offer Declined + Application OfferDeclined
Any active reviewer stage: HR/Manager Reject → Application Rejected (+ cancel pending interview/offer)
Applied/Screening/ManagerReview/Interview: Candidate Withdraw → Withdrawn (+ pending interview stale)
```

See [STATE-MACHINE.md](STATE-MACHINE.md) for the per-table transition tables,
[BUSINESS-RULES.md](BUSINESS-RULES.md) for enforceable rules, and [TEST-MATRIX.md](TEST-MATRIX.md) for
the test traceability.

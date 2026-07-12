# RecruitPro — UAT Documentation Suite

This folder is the **User Acceptance Testing** package for the full RecruitPro product. It was derived by
reading the implementation (backend controllers/services/enums/validators, frontend routes/guards/forms,
`init.sql`) and the `docs/source-of-truth/*` contracts — every case traces to a real requirement, route,
API, rule, or constraint.

## What's here

| File | Purpose |
|---|---|
| [`UAT_TEST_PLAN.md`](UAT_TEST_PLAN.md) | Objective, scope, roles, environment, entry/exit criteria, strategy, and the **coverage report** (computed counts). |
| [`UAT_TEST_CASES.md`](UAT_TEST_CASES.md) | **378 executable test cases** across 22 suites, each with full fields (steps, expected UI/API/data/notification results). |
| [`UAT_TEST_DATA.md`](UAT_TEST_DATA.md) | Seed accounts/roles/master/business data, login facts, data-prep for states not in the seed, negative/boundary data sets, cleanup & isolation. |
| [`UAT_API_COVERAGE.md`](UAT_API_COVERAGE.md) | All **141 endpoints** → case IDs + coverage status. |
| [`UAT_TRACEABILITY_MATRIX.md`](UAT_TRACEABILITY_MATRIX.md) | 9 matrices (module→route/API, role→perm→action, requirement/API/state/flow/entity/risk → case). |
| [`UAT_OPEN_QUESTIONS.md`](UAT_OPEN_QUESTIONS.md) | 25 requirement gaps / FE-BE mismatches + 4 known production defects. |
| `PRODUCTION_UAT_BUG_REPORT.md` | (existing) prior production UAT run — 4 known defects (BUG-UAT-001..004). |
| `PRODUCTION_UAT_EXECUTION_LOG.md` | (existing) prior execution log — the format to follow when recording results. |
| `v4-workflow-automation-uat.md` | (existing) focused checklist for the v4 automation feature. |
| `evidence/` | Screenshots / captures per case. |

## How to read a test case

Cases live in [`UAT_TEST_CASES.md`](UAT_TEST_CASES.md), grouped by module (prefix `UAT-<MODULE>-NNN`). Each
block carries the full field set (task §7): Module/Sub, Type, Priority, Severity, Source, Actor/Perm,
Preconditions, Data, Steps, Expected **UI / API / Data / Audit-Notification**, Post/Cleanup, Related, Auto,
Notes. **Global defaults** (environment, accounts, cleanup, error-contract expectations) are stated once at
the top of that file — read them first. Resolve every `<…_SEED_ACCOUNT>` placeholder and seed ID from
[`UAT_TEST_DATA.md`](UAT_TEST_DATA.md).

## How to use this in the execution phase

1. **Read** `UAT_TEST_PLAN.md` (scope, entry criteria, priority) and the top of `UAT_TEST_CASES.md`.
2. **Confirm environment & blockers** — especially **BUG-UAT-001** (auth session). If it's still live on
   production, run authenticated-UI steps locally and verify the API leg directly.
3. **Prepare data** per `UAT_TEST_DATA.md` (create `[UAT]`-marked entities for states not in the seed).
4. **Execute** in priority order: P0 → P1 → P2 → P3. Run the API-wide template cases (`UAT-API-*`) against
   each endpoint listed in `UAT_API_COVERAGE.md`.
5. **Record** results, **raise defects** for failures, and **clean up** (below).
6. **Report** against the exit criteria; obtain sign-off in `UAT_TEST_PLAN.md` §18.

## Recording Actual Result (rule)

For each executed case, record in the execution log:

```
Case: UAT-<MODULE>-NNN
Run by / date / environment
Result: PASS | FAIL | BLOCKED | N/A
Actual — UI:   <what happened on screen>
Actual — API:  <method path → status + error.code + traceId (for failures)>
Actual — Data: <side-effect / row state observed, or "not verifiable">
Evidence:      evidence/UAT-<MODULE>-NNN-*.png  (+ request/response capture)
Notes:         <deviations, blockers, open-question refs>
```

- **PASS** only if UI **and** API **and** data expectations all match.
- **BLOCKED** must name the blocker (e.g. BUG-UAT-001, AI provider unavailable, MinIO/mail).
- For **FAIL**, capture the `traceId` from the error envelope and full repro.
- Cases flagged **[KNOWN BUG]** are expected to FAIL until the defect is fixed — record as a confirmed
  regression against the existing bug ID, not a new defect.

## Saving evidence (rule)

- Store under `docs/uat/evidence/` with the case ID in the filename (`UAT-APP-007-duplicate-409.png`).
- Include the request/response for API assertions (redact tokens/passwords/keys/connection strings).
- **Never** capture secrets. For 500s, capture only the `{ traceId }` and the server-side correlation.

## Logging a defect (rule)

Add to `PRODUCTION_UAT_BUG_REPORT.md` (or the tracker) with:

```
BUG-<AREA>-NNN
Severity (Critical/High/Medium/Low) · Priority (P0–P3)
Module · Role/account · Environment · URL/endpoint
Preconditions · Test data · Reproducibility
Steps to reproduce (numbered)
Expected result · Actual result (with status + error.code + traceId)
Impact · Suspected cause (if known) · Evidence files · Related case IDs
```

Map each defect back to the failing case ID(s) and any relevant open question in
[`UAT_OPEN_QUESTIONS.md`](UAT_OPEN_QUESTIONS.md).

## Ground rules (safety)

- Login by **username**, shared demo password `Password@123` (public test data in the repo).
- **Never** delete/mutate seed personas or drive seed applications to terminal states — create fresh
  `[UAT]`-marked data instead.
- **Restore** any global state you change (RBAC matrix, automation mode, a user's status/roles) immediately
  after the case.
- Do not leave a `[UAT]` automation workflow in **Live** mode (it sends real notifications).
- This suite is **documentation** — executing it against production changes live data; follow cleanup
  (`UAT_TEST_DATA.md` §9) and isolation (§10).

## At a glance

- **378** test cases · **141** API endpoints mapped · **5** roles · **27** backend permissions · **6** state
  machines · **10** E2E scenarios · **25** open questions · **4** known production defects.
- Coverage report with full breakdowns: [`UAT_TEST_PLAN.md`](UAT_TEST_PLAN.md) § Coverage report.

# v2 Hardening Report - 2026-06-30

> **Historical.** Superseded by the final phase-2 refactor (ranking = single source of truth). See
> `03-v2-ai-recruitment-copilot.md` → "Current State (final)". Notably, the artifact-history and
> prompt-template panels described below were later removed from the UI, and the shortlist feature was
> deleted; candidate search and email draft are deprecated (no AI). The final flow is screening-only
> ranking → explicit Pass CV → Head Review.

## Executive Summary

This hardening pass cleaned up the two build warnings called out by the user, continued v2 roadmap implementation, added read/admin surfaces for persisted AI artifacts and prompt templates, wired provider-backed structured JSON generation behind deterministic fallback, and added frontend acceptance E2E coverage. RecruitPro can now show the latest saved AI fit-analysis snapshot on the application review detail screen, expose generated artifact history in AI Copilot, manage prompt templates from the same Copilot workspace, safely use an enabled provider for the existing v2 Copilot actions, and verify those user workflows through deterministic Playwright tests.

## Done

### Build Warning Cleanup

- Removed repository projection nullable warnings in:
  - `RecruitPro.Infrastructure/Repositories/CopilotRepository.cs`
  - `RecruitPro.Infrastructure/Repositories/ApplicationRepository.cs`
- Removed a Copilot scoring nullable warning by making `candidate.CvSummary` access null-safe.
- Fixed Vite chunk-size warning by lazy-loading route screens in:
  - `recruit-pro-internal/src/routes/public.routes.tsx`
  - `recruit-pro-internal/src/routes/candidate.routes.tsx`
  - `recruit-pro-internal/src/routes/hr.routes.tsx`
- Resolved `NU1903` by upgrading `AutoMapper` from `12.0.0` to `15.1.3` and removing the deprecated `AutoMapper.Extensions.Microsoft.DependencyInjection` package.

### v2 Backend Hardening

- Added read APIs:
  - `GET /api/copilot/applications/{applicationId}/fit-analysis/latest`
  - `GET /api/copilot/artifacts`
- Added service methods with ownership-aware access:
  - latest persisted fit analysis for an application
  - current-user generated artifact history with optional filters
- Added repository methods:
  - `GetLatestFitAnalysisAsync`
  - `GetGeneratedArtifactsAsync`
- Added response DTOs:
  - `CandidateFitAnalysisSnapshotDto`
  - `CopilotGeneratedArtifactDto`
- Added a unit test for mapping and returning latest persisted fit analysis.
- Added API integration/RBAC tests for:
  - latest fit-analysis endpoint
  - artifact history endpoint and filters
  - prompt-template list/create endpoints
- Added provider-backed structured JSON generation for:
  - natural-language candidate search
  - candidate fit analysis
  - interview question generation
  - shortlist suggestions
  - HR email drafts
- Added provider fallback handling for disabled/missing config, provider exception/timeout, invalid JSON, and validation failure.
- Provider calls reuse active current-user prompt templates by `templateType` when available.
- Provider success/fallback metadata is persisted in generated artifacts and fit-analysis snapshots.
- Added unit tests for provider disabled, valid provider JSON, invalid JSON, provider exception, missing required fields, and provider fit-analysis persistence.

### v2 Frontend Hardening

- Added frontend copilot service methods for latest fit analysis and artifact history.
- Added an `AI fit analysis` card to `CandidateReviewDetailScreen`.
- The card shows:
  - fit label
  - total score
  - confidence score
  - generated timestamp
  - summary
  - strengths
  - gaps
  - evidence
  - audit/provider/fallback metadata
- Added artifact history UI to `AiCopilotScreen`.
- Added prompt-template management UI to `AiCopilotScreen`:
  - list templates
  - create template
  - view selected template details
  - show active status and timestamps
- Clarified provider/fallback metadata labels in:
  - artifact history items
  - latest fit-analysis card
- Added Playwright E2E coverage for:
  - latest fit-analysis card with data
  - latest fit-analysis empty state
  - latest fit-analysis provider/fallback metadata
  - artifact history load, preview, provider metadata, fallback metadata, current-job filter, type filter, empty state, and error state
  - prompt-template list/create/detail and required-field validation
  - absence of unsupported prompt-template edit/delete/versioning UI

### Documentation Updated

- `03-v2-ai-recruitment-copilot.md`
- `08-api-changes.md`
- `09-frontend-changes.md`
- `13-implementation-backlog.md`
- `11-ai-prompts-and-tools.md`
- `15-v2-hardening-report.md`

## Current State

- v2 P0 foundation endpoints exist and return deterministic fallback outputs.
- When provider config is enabled, v2 generation first requests structured JSON and only accepts it after parse/validation/normalization.
- Deterministic fallback remains the stable public contract for disabled providers and provider/JSON failures.
- Generated search/question/shortlist/email artifacts are persisted.
- Fit-analysis outputs are persisted per candidate/application and can be read back.
- Review detail can display the latest persisted fit analysis.
- Artifact history is visible from `AiCopilotScreen` with current-job and artifact-type filtering.
- Prompt-template list/create/detail is available in `AiCopilotScreen`; edit/delete remains unavailable because the backend API does not currently expose those operations.
- Frontend acceptance E2E validates the existing v2 user workflows with Playwright route mocks and no real provider calls.
- Frontend production build is split into smaller chunks and no longer emits the Vite >500 kB warning.
- `NU1903` no longer appears after the AutoMapper package decision.

## Verification

- `dotnet build RecruitProInternal.sln --no-restore` passed.
- `dotnet test RecruitPro.Tests\RecruitPro.Tests.csproj --no-build --filter "FullyQualifiedName~Copilot"` passed: 21/21.
- `dotnet test RecruitPro.Tests\RecruitPro.Tests.csproj --no-build --filter "FullyQualifiedName~ApiIntegrationTests"` passed: 20/20.
- `dotnet test RecruitPro.Tests\RecruitPro.Tests.csproj --no-build --filter "FullyQualifiedName!~Integration&FullyQualifiedName!~Repository&FullyQualifiedName!~Container"` passed: 251/251.
- `npx playwright test e2e/ai-copilot-v2-acceptance.e2e.ts` passed: 7/7.
- `npm run e2e` passed: 30/30.
- `npm run build` passed and did not emit the previous chunk-size warning.

## Remaining Warnings

- `NU1903` is resolved.
- Final required `dotnet build RecruitProInternal.sln --no-restore` verification passed with 0 warnings.
- No Vite chunk-size warning remains in `npm run build`.

## Doing / Next In Progress

The v2 hardening track is now in the read-side integration stage:

- persisted AI fit snapshots are visible in application review detail
- artifact history is visible in AI Copilot
- prompt templates can be listed/created/inspected in AI Copilot
- provider-backed structured JSON is active behind the deterministic fallback contract
- Playwright acceptance tests cover the v2 read/admin/provider-metadata workflows
- docs and backlog now point to the remaining v2 hardening work instead of persistence/read-surface foundations

## Recommended Next Work

1. Team acceptance review for the v2 AI Copilot workflows and E2E evidence.
2. Add artifact drill-down/edit/delete only after backend APIs exist for those operations.
3. Add prompt-template edit/delete/versioning only after backend APIs exist.
4. Keep frontend E2E coverage updated when backend APIs expand.
5. Start v3 only after the team accepts the remaining v2 gaps above, because v3 agents should reuse these v2 tools and audit surfaces.

## Acceptance E2E Decision

- E2E framework: Playwright already exists in `recruit-pro-internal`.
- Test file: `e2e/ai-copilot-v2-acceptance.e2e.ts`.
- Mocking strategy: seeded HR session plus Playwright `/api/**` route interception; no live backend and no real external AI provider.
- Covered test data:
  - application with persisted fit-analysis snapshot
  - application without fit-analysis snapshot
  - provider artifact
  - fallback artifact
  - artifact with missing provider/model metadata
  - empty artifact history
  - artifact API error state
  - prompt-template list and create success
  - prompt-template client validation for missing required fields
- Contract check result: no `CONTRACT_MISMATCH` found for latest fit-analysis, artifacts, or prompt-template list/create. Prompt-template detail is currently a UI detail panel from list data, not a separate backend detail endpoint.
- `BACKEND_API_NOT_AVAILABLE`: artifact edit/delete.
- `BACKEND_API_NOT_AVAILABLE`: prompt-template edit/delete/versioning.

## Provider Structured JSON Decision

- Provider method added: `IAiCopilotProvider.TryCreateStructuredJsonAsync`.
- Provider implementation: `AiCopilotProvider` requests JSON mode through the existing compatible API helper and returns normalized JSON plus provider/model metadata.
- Fallback triggers: provider disabled, missing API key, provider exception/timeout, non-success response, empty output, invalid JSON, or missing required fields.
- Accepted JSON shapes:
  - `candidate_search`: normalized intent, query, extracted filters, candidate results
  - `fit_analysis`: job id and candidate fit analyses
  - `interview_questions`: job id, optional candidate id, focus, questions
  - `shortlist_suggestion`: job id and candidate suggestions
  - `email_draft`: application id, template type, subject, body, evidence
- Public DTO contracts were not changed; provider metadata is surfaced through the existing `Ai` metadata object.

## AutoMapper Decision

- Previous version: `AutoMapper` 12.0.0 and `AutoMapper.Extensions.Microsoft.DependencyInjection` 12.0.0.
- Applied target: `AutoMapper` 15.1.3.
- Rationale: `15.1.3` resolves the high-severity advisory while staying on a .NET 8-compatible line; the deprecated DI extension package was removed because DI registration is now in AutoMapper core.
- Breaking-change handling:
  - `Program.cs` now uses the newer `AddAutoMapper` overload.
  - test `MapperConfiguration` constructors now pass `NullLoggerFactory.Instance`.
- Result: build/tests pass and `NU1903` no longer appears.

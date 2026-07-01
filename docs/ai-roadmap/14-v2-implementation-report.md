# v2 Implementation Report - 2026-06-30

> **Historical.** This report describes the initial 2026-06-30 foundation slice. Phase 2 was later
> refactored to make **ranking the single source of truth** and is now closed. For the shipped final
> state see `03-v2-ai-recruitment-copilot.md` → "Current State (final)". Key deltas since this report:
> ranking is screening-only with Vietnamese fit built in; duplicate ranking is suppressed via
> `input_hash`; a **Pass CV → Head Review** action was added; **candidate search** and **email draft**
> were deprecated (no AI); the **shortlist** feature was **removed**; interview questions are cached
> after first generation; and the artifact-history / prompt-template panels are no longer in the UI.

## Summary

The v2 AI Recruitment Copilot foundation is now implemented as a deterministic, auditable slice. It adds structured copilot tools for HR/Manager users, persists generated outputs where appropriate, and keeps the main ATS workflow unchanged.

## Implemented Scope

- Backend endpoints for:
  - natural-language candidate search
  - candidate fit analysis
  - interview question generation
  - shortlist suggestions
  - HR email drafting
  - prompt-template list/create
- AI metadata on v2 outputs:
  - `auditId`
  - `artifactId` for persisted generated artifacts
  - `fallbackUsed`
  - provider/model names
  - warnings
- Persistence:
  - `candidate_fit_analyses` stores per-candidate fit snapshots.
  - `copilot_generated_artifacts` stores generated search/question/shortlist/email outputs.
  - `copilot_prompt_templates` stores reusable recruiter prompts.
- Frontend:
  - `AiCopilotScreen` has a compact v2 tool panel.
  - Frontend copilot service exposes the new prompt-template API.
  - Tool results display audit/artifact identifiers.

## Main Files Changed

- API: `RecruitPro.API/Controllers/CopilotController.cs`
- Application DTOs/interfaces/service:
  - `RecruitPro.Application/DTOs/Request/Copilot/CopilotPromptRequest.cs`
  - `RecruitPro.Application/DTOs/Response/Copilot/CopilotDtos.cs`
  - `RecruitPro.Application/Interfaces/IServices/ICopilotService.cs`
  - `RecruitPro.Application/Services/CopilotService.cs`
- Domain/Infrastructure:
  - `RecruitPro.Domain/Entities/CandidateFitAnalysis.cs`
  - `RecruitPro.Domain/Entities/CopilotGeneratedArtifact.cs`
  - `RecruitPro.Domain/Entities/CopilotPromptTemplate.cs`
  - `RecruitPro.Infrastructure/Data/AppDbContext.cs`
  - `RecruitPro.Infrastructure/Repositories/CopilotRepository.cs`
  - `RecruitPro.Infrastructure/Migrations/20260630000000_AddCopilotV2Artifacts.cs`
  - `RecruitPro.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`
- Frontend:
  - `recruit-pro-internal/src/services/http/endpoints.ts`
  - `recruit-pro-internal/src/services/copilot/copilotService.ts`
  - `recruit-pro-internal/src/pages/hr/AiCopilotScreen.tsx`
- Tests/docs:
  - `RecruitPro.Tests/ApplicationLayerServiceUnitTests.cs`
  - `docs/ai-roadmap/03-v2-ai-recruitment-copilot.md`
  - `docs/ai-roadmap/13-implementation-backlog.md`

## Verification

- `dotnet build RecruitProInternal.sln --no-restore` passed.
- `dotnet test RecruitPro.Tests\RecruitPro.Tests.csproj --no-build --filter "FullyQualifiedName~CopilotServiceUnitTests"` passed: 5/5.
- `dotnet test RecruitPro.Tests\RecruitPro.Tests.csproj --no-build --filter "FullyQualifiedName!~Integration&FullyQualifiedName!~Repository&FullyQualifiedName!~Container"` passed: 242/242.
- `npm run build` passed for `recruit-pro-internal`.

## Known Warnings

- `AutoMapper 12.0.0` has a known high-severity vulnerability warning (`NU1903`).
- Existing nullable dereference warnings remain in repository projection code.
- Vite reports a chunk-size warning because the production bundle is larger than 500 kB.

## Remaining v2 Work

- Add provider-backed structured JSON generation behind the deterministic fallback contract.
- Add integration/RBAC tests for every new copilot endpoint.
- Add review-screen surfaces for persisted fit snapshots and generated artifact history.
- Add a full prompt-template management UI instead of service-only API support.

# Frontend Changes

## Strategy

Extend the current React app by role-based screens and panels. Do not create a separate AI frontend.

Reuse:

- route guards
- permission hooks
- current HR AI copilot screen
- existing dashboard and candidate review screens

## v2 (as shipped)

### HR

`recruit-pro-internal/src/pages/hr/AiCopilotScreen.tsx` (table-first). Ranking is the primary action.

- ranking table shows, per candidate: rank, name, education, skills, Vietnamese AI summary, fit label,
  confidence, evidence, strengths/gaps, score, and provider/fallback metadata
- per-row AI tools: **fit analysis** and **interview questions** only
- candidate selection checkboxes + **"Chuyển sang Head Review"** (Pass CV) button; the button only acts
  on Screening candidates and, after moving them, refreshes the pool so they leave the list
- "Tiêu chí chưa thay đổi…" info message when a re-rank reuses the latest matching session
- assistant drawer holds the scope-guarded chat (unrelated prompts get a Vietnamese refusal)
- criteria builder + tool-result popups render via `createPortal(document.body)` at `z-[100]`
- add fit-analysis cards to `CandidateReviewDetailScreen` (latest persisted snapshot, with
  provider/fallback metadata) and `CandidateProfileScreen`
- route-level lazy loading is implemented to avoid oversized production chunks

Removed / not shipped in the UI:

- NL **search composer** — deprecated
- **shortlist builder** — feature removed entirely
- **email draft** editor — deprecated
- **artifact history** panel and **prompt-template** management panel — not present (their backend
  endpoints still exist but the screen does not use them)

Playwright coverage: `e2e/ai-copilot-ranking.e2e.ts` (education/skills rendering, not-ranked state,
run-review, deprecated tools absent, Pass CV → Head Review) and the fit-analysis card cases in
`e2e/ai-copilot-v2-acceptance.e2e.ts` (V2-001/002/007). The V2-003..006 acceptance cases target the
removed artifact-history/prompt-template panels and are obsolete.

### Manager

- add read-only AI rationale on review queue screens

### Candidate

- optional job-match explanation on [DashboardCandidateScreen](/D:/FPT_HocTap/Semester%208/PRN232/recuit-pro/recruit-pro-fe/recruit-pro-fe/src/pages/candidate/DashboardCandidateScreen.tsx)

### Admin

- prompt template management page or expanded admin view; minimal HR-side management is implemented in `AiCopilotScreen`

## v3

### HR

- agent task launcher
- agent run timeline
- approval modal

### Candidate

- candidate assistant chat

### Manager

- approvals and analytics Q&A panel

### Admin

- agent definition and policy screens

## v4

### Admin / HR Ops

- workflow builder
- workflow templates gallery
- execution logs and failure inspector
- MCP tool catalog

## v5

### Admin

- AI operations dashboard
- prompt version compare
- provider routing controls
- evaluation results screen

### Manager

- talent intelligence dashboard

## UX Rules

- show when AI is thinking, failed, or used fallback
- separate "Generate" from "Apply/Send"
- display evidence and confidence, not only prose
- use role-aware empty states and permission messaging

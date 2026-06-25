# Frontend Changes

## Strategy

Extend the current React app by role-based screens and panels. Do not create a separate AI frontend.

Reuse:

- route guards
- permission hooks
- current HR AI copilot screen
- existing dashboard and candidate review screens

## v2

### HR

- enhance [AiCopilotScreen](/D:/FPT_HocTap/Semester%208/PRN232/recuit-pro/recruit-pro-fe/recruit-pro-fe/src/pages/hr/AiCopilotScreen.tsx) with:
  - NL search composer
  - explanation drawer
  - shortlist builder
  - email draft approval/edit panel
  - interview question action
- add fit-analysis cards to:
  - [CandidateReviewDetailScreen](/D:/FPT_HocTap/Semester%208/PRN232/recuit-pro/recruit-pro-fe/recruit-pro-fe/src/pages/hr/CandidateReviewDetailScreen.tsx)
  - [CandidateProfileScreen](/D:/FPT_HocTap/Semester%208/PRN232/recuit-pro/recruit-pro-fe/recruit-pro-fe/src/pages/hr/CandidateProfileScreen.tsx)

### Manager

- add read-only AI rationale on review queue screens

### Candidate

- optional job-match explanation on [DashboardCandidateScreen](/D:/FPT_HocTap/Semester%208/PRN232/recuit-pro/recruit-pro-fe/recruit-pro-fe/src/pages/candidate/DashboardCandidateScreen.tsx)

### Admin

- prompt template management page

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

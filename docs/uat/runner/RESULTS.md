# RecruitPro Staging UAT — Execution Log

- **Environment:** https://www.recruitpro.site/api
- **Run at:** 2026-07-15T03:27:49.706Z
- **Runner:** automated (docs/uat/runner)

## Summary

| Total | PASS | FAIL | BLOCKED | N/A |
|---|---|---|---|---|
| 5 | 5 | 0 | 0 | 0 |

## By suite

| Suite | PASS | FAIL | BLOCKED | N/A |
|---|---|---|---|---|
| AI | 5 | 0 | 0 | 0 |

## Cases

| Case | Pri | Result | API | Notes |
|---|---|---|---|---|
| UAT-AI-001 | P2 | PASS | HR→200, candidate→403, guest→401 |  |
| UAT-AI-006 | P3 | PASS | unknown ranking session → 404 ENTITY_NOT_FOUND |  |
| UAT-AI-016 | P0 | PASS | foreign/unknown conversation → 404 ENTITY_NOT_FOUND | cross-HR real-session IDOR needs two live sessions — see MANUAL-CHECKLIST |
| UAT-AI-017 | P2 | PASS | unknown-job pool → 404 JOB_NOT_FOUND |  |
| UAT-AI-015 | P1 | PASS | copilot/jobs: no apiKey/systemPrompt/secret fields |  |

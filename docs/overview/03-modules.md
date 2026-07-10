# 03. Modules

## 1. Authentication

Purpose:

- Candidate and internal login
- Token issuance and rotating refresh (the `RefreshToken` table is read and written)
- Session access control

Main entities:

- `User`
- `Role`
- `Permission`
- `RefreshToken`

Current implementation:

- JWT-based auth service: 15-min access tokens with a `token_version` claim, plus opaque 7-day refresh tokens stored as SHA-256 hashes and rotated (single-use) via `POST /api/auth/refresh`
- Role-based authorization attributes in business controllers; fine-grained permission checks are used only in the SysAdmin area
- Account deactivation bumps `token_version` and deletes the user's refresh tokens, invalidating live access tokens immediately

## 2. Candidate

Purpose:

- Candidate registration and self-service profile management

Main entities:

- `CandidateProfile`
- `CandidateResume`
- `CandidateSkill`
- `CandidateProject`
- `CandidateProfileSection`
- `CandidateProfileSectionItem`

Business rules:

- Candidate can only manage own data
- Resume upload can update profile source data

APIs:

- `GET /api/candidate/profile`
- `PUT /api/candidate/profile`
- `POST /api/candidate/profile/resume`

## 3. Job

Purpose:

- Public job browsing and internal job management

Main entities:

- `Job`
- `JobSkill`
- `Department`
- `Skill`

Business rules:

- Public job browsing is separate from internal management
- Job status controls workflow visibility

## 4. Application

Purpose:

- Apply, review, score, and progress candidate applications

Main entities:

- `Application`
- `ApplicationOffer`
- `ApplicationOfferBenefit`

Business rules:

- Candidate can apply only as themselves
- HR can review and change workflow status

## 5. Interview

Purpose:

- Scheduling and management of interviews

Main entities:

- `Interview`

Business rules:

- Interview is tied to an application
- Status changes are tracked and permission-gated

## 6. Notification

Purpose:

- In-app notifications and realtime delivery

Main entities:

- `Notification`

## 7. Dashboard

Purpose:

- Candidate dashboard and internal dashboards

Main entities:

- Derived from `Job`, `Application`, `Interview`, and analytics DTOs

## 8. Search and Recommendation

Purpose:

- Job search
- Candidate matching
- Recommendation and semantic discovery

Main entities:

- `CandidateProfile`
- `Job`
- embedding-related runtime data

## 9. AI / Copilot

Purpose:

- Resume parsing
- Embedding generation
- Semantic scoring
- Ranking assistant

Main entities:

- `CopilotConversation`
- `CopilotMessage`
- `CopilotRankingSession`
- `CopilotRankingResult`
- `CopilotSavedRule`
- `CopilotCandidateTag`

## 10. Administration

Purpose:

- Internal SysAdmin operations; this is the only area with fine-grained permission-based enforcement

Main entities:

- `User`
- `Role`
- `Permission`
- `SystemLog`


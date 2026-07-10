# 05. API

## API Conventions

- Base prefix: `/api`
- Response envelope is used consistently in the API contract
- Authorization varies by endpoint and role

## Auth Endpoints

- `POST /api/auth/login`
- `POST /api/auth/candidate/login`
- `POST /api/auth/internal/login`
- `POST /api/auth/refresh` — body `{ "refreshToken": "..." }`; validates and rotates the DB-stored refresh token (single-use: old token deleted, new pair issued) and returns a fresh `{ accessToken, refreshToken }` in the standard envelope. Returns 401 if the token is missing/expired/unknown or the account is not Active.
- `GET /api/auth/me` — not implemented (no such endpoint).
- `POST /api/auth/logout` — not implemented; logout is client-side only (the client discards its tokens). The server revokes refresh tokens only on account deactivation and retires used ones on rotation.

## Candidate Endpoints

- `POST /api/candidates/register`
- `GET /api/candidate/profile`
- `PUT /api/candidate/profile`
- `PUT /api/candidate/profile/skills`
- `POST /api/candidate/profile/experience`
- `PUT /api/candidate/profile/experience/{experienceId}`
- `DELETE /api/candidate/profile/experience/{experienceId}`
- `POST /api/candidate/profile/resume`
- `GET /api/candidate/dashboard`
- `GET /api/candidate/applications`
- `POST /api/candidate/applications/{applicationId}/withdraw`
- `POST /api/candidate/applications/{applicationId}/accept-offer`

## Job Endpoints

- `GET /api/jobs`
- `GET /api/jobs/filters`
- `GET /api/jobs/{jobId}`
- `GET /api/jobs/{jobId}/applications`
- `GET /api/jobs/{jobId}/applications/recent`
- `GET /api/jobs/{jobId}/statistics`
- `PATCH /api/jobs/{jobId}/status`

## Internal Job Endpoints

- `GET /api/hr/jobs`
- `POST /api/hr/jobs`
- `PATCH /api/hr/jobs/{jobId}`
- `PATCH /api/hr/jobs/{jobId}/status`
- `DELETE /api/hr/jobs/{jobId}`

## Internal Candidate and Application Endpoints

- `GET /api/hr/candidates`
- `GET /api/hr/candidates/{candidateId}`
- `PATCH /api/hr/candidates/{candidateId}`
- `GET /api/hr/applications`
- `GET /api/hr/applications/{applicationId}`
- `PATCH /api/hr/applications/{applicationId}/status`
- `GET /api/hr/applications/{applicationId}/cv`
- `POST /api/hr/applications/{applicationId}/send-email`

## Interview Endpoints

- `GET /api/hr/interviews`
- `GET /api/hr/interviews/schedule-data`
- `POST /api/hr/interviews`
- `PATCH /api/hr/interviews/{interviewId}/status`
- `DELETE /api/hr/interviews/{interviewId}`

## Lookup and Notification

- `GET /api/departments`
- `GET /api/skills`
- `GET /api/notifications`

## Copilot Endpoints

- `GET /api/copilot/jobs`
- `POST /api/copilot/conversations`
- `GET /api/copilot/jobs/{jobId}/candidates`
- `POST /api/copilot/conversations/{conversationId}/rankings`
- `GET /api/copilot/conversations/{conversationId}`
- `GET /api/copilot/ranking-sessions/{rankingSessionId}`
- `POST /api/copilot/jobs/{jobId}/rules`
- `GET /api/copilot/jobs/{jobId}/rules`
- `PATCH /api/copilot/rules/{ruleId}`
- `POST /api/copilot/candidates/{candidateUserId}/tags`

## API Notes

- Some endpoints visible in the frontend contract may not yet exist exactly as described in the backend source.
- If an endpoint cannot be confirmed from source code, it should be treated as unconfirmed in downstream documentation.


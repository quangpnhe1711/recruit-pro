# 09. Authentication and Authorization

## Authentication

Confirmed mechanisms:

- JWT authentication (short-lived access tokens, 15 minutes, carrying a `token_version` claim)
- Working refresh-token rotation via `POST /api/auth/refresh`
- Role-aware login endpoints

Refresh tokens are opaque random strings with a 7-day lifetime, one per login. They are stored in the database as SHA-256 hashes (the raw value is only ever returned to the client) and rotated on each refresh: the used token is deleted and a new `{ accessToken, refreshToken }` pair is issued. Refresh returns 401 if the token is missing, expired, unknown, or the account is not Active.

There is no `GET /api/auth/me` and no `POST /api/auth/logout` endpoint. Logout is client-side only — the client discards its tokens. The server revokes tokens only on account deactivation: SysAdmin `PATCH /api/sysadmin/users/{id}/status` to Inactive/Blocked bumps `users.token_version` and deletes all of that user's refresh tokens, so every live access token fails immediately (checked per-request in `JwtExtension.OnTokenValidated`) and no new one can be minted.

## Authorization

Observed access model:

- Candidate endpoints are protected for candidate role
- Internal endpoints are protected for HR/Manager/SystemAdmin
- Shared routes are further gated by permissions in frontend and backend design

## Roles

- Candidate
- HR
- Manager
- SystemAdmin

## Permissions

The repository contains a permission matrix using resource-action naming such as:

- `job:create`
- `application:view-all`
- `interview:create`
- `candidate:update-own-profile`

## Claims

Source indicates JWT and claim-based access, but exact claim schema should be treated as implementation-specific unless confirmed in token generation code.

## Middleware and Filters

Confirmed in source tree:

- exception middleware
- validation filter
- route guards on frontend

## Authorization Flow

1. User signs in
2. API returns a 15-minute access JWT and an opaque refresh token (hashed in DB); the client later calls `POST /api/auth/refresh` to rotate them
3. Client stores token
4. Protected endpoints validate token and role/permission
5. Frontend guards route and action visibility

## Known Gaps

- Some endpoints in current codebase are still broader than ideal RBAC target
- Shared routes may expose internal actions unless button-level permission checks are used


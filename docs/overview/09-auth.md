# 09. Authentication and Authorization

## Authentication

Confirmed mechanisms:

- JWT authentication
- Refresh token support
- Role-aware login endpoints

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
2. API returns JWT and refresh token
3. Client stores token
4. Protected endpoints validate token and role/permission
5. Frontend guards route and action visibility

## Known Gaps

- Some endpoints in current codebase are still broader than ideal RBAC target
- Shared routes may expose internal actions unless button-level permission checks are used


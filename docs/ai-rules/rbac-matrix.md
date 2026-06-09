# RecruitPro RBAC Matrix

## Purpose

This document defines the target authorization and UI visibility model for RecruitPro, derived from:

- Existing frontend screens, routes, menus, tables, forms, buttons, and modals
- Existing backend controllers, endpoints, services, and business workflows
- Existing role usage in API authorization attributes and UI flow assumptions

This file is intended to drive:

- Backend authorization rules
- Frontend route guards
- Frontend menu rendering
- Frontend button visibility
- Frontend action visibility

This document does **not** modify code.

---

## Roles

| Role | Description |
| --- | --- |
| `Candidate` | External applicant using candidate-facing flows |
| `HR` | Internal recruiter / HR operator |
| `Manager` | Internal manager role; inherits all HR permissions |
| `SystemAdmin` | Platform-wide internal administrator with full internal visibility and control |

## Inheritance Model

| Role | Inherits |
| --- | --- |
| `Candidate` | None |
| `HR` | None |
| `Manager` | All `HR` permissions |
| `SystemAdmin` | All `HR` and `Manager` permissions |

## Authorization Design Principles

1. Authorization is permission-based, not role-name-based.
2. Roles are just bundles of permissions.
3. Candidate permissions are self-scoped wherever applicable.
4. Internal permissions are capability-scoped by resource:
   - `auth`
   - `dashboard`
   - `job`
   - `candidate`
   - `application`
   - `interview`
   - `lookup`
   - `system`
5. UI visibility must align with backend authorization.
6. Shared screens may exist, but internal actions on shared screens must still be permission-gated.

---

## Current-State Observations

These affect how the target RBAC should be interpreted:

1. Backend currently protects internal APIs with `[Authorize(Roles = "HR,Manager,SystemAdmin")]`, so `Manager` and `SystemAdmin` already behave as internal roles.
2. Candidate APIs are protected with `[Authorize(Roles = "Candidate")]`.
3. Several sensitive endpoints are currently public but should be internal-only in the target model:
   - `GET api/jobs/{jobId}/applications`
   - `GET api/jobs/{jobId}/applications/recent`
   - `GET api/jobs/{jobId}/statistics`
   - `PATCH api/jobs/{jobId}/status`
4. `JobDetailScreen` is on a shared route (`/jobs/:jobId`) but currently renders internal actions like `Edit Job`, `View Applications`, and `Close Posting`. These actions must be permission-gated even if the route remains shared.
5. Some frontend links use non-canonical paths such as `/candidate/register` while actual route declarations use `/register`. This matrix uses canonical route definitions from `src/routes`.
6. No manager-exclusive workflow exists in the current codebase. `Manager` currently inherits the HR capability set.
7. No SystemAdmin-exclusive UI exists in the current codebase, but backend authorization already recognizes `SystemAdmin`, so this matrix reserves full-access visibility for that role.

---

## Frontend Inventory

### Route Inventory

| Route | Screen | Audience Classification | Notes |
| --- | --- | --- | --- |
| `/home` | `LandingPageScreen` | Shared/Public | Marketing / landing |
| `/login` | `CandidateLoginScreen` | Candidate/PublicOnly | Candidate login |
| `/register` | `CandidateRegisterScreen` | Candidate/PublicOnly | Candidate registration |
| `/internal/login` | `InternalLoginScreen` | Internal/PublicOnly | Internal login |
| `/jobs` | `JobsRouteScreen` | Shared | Candidate listing or internal job management |
| `/jobs/:jobId` | `JobDetailScreen` | Shared | Shared route with mixed public/internal actions |
| `/candidate/dashboard` | `DashboardCandidateScreen` | Candidate-only | Candidate dashboard |
| `/candidate/my-applications` | `MyApplicationScreen` | Candidate-only | Candidate applications |
| `/candidate/profile/*` | `CandidateProfileAndCVManagementScreen` | Candidate-only | Candidate profile and CV |
| `/hr/dashboard` | `HrDashboardScreen` | Internal-only | Internal dashboard |
| `/hr/jobs/create` | `JobCreatingScreen` | Internal-only | Create job workflow |
| `/hr/candidates` | `CandidateListScreen` | Internal-only | Candidate management |
| `/hr/applications` | `CandidateApplicationScreen` | Internal-only | Application management |
| `/hr/interviews` | `JobInterviewListScreen` | Internal-only | Interview list |
| `/hr/interviews/schedule` | `InterviewScheduleScreen` | Internal-only | Interview scheduling |

### Navigation Inventory

#### Public Header

| Item | Destination | Type |
| --- | --- | --- |
| Home | `#home` | Marketing anchor |
| Careers | `#careers` | Marketing anchor |
| About Us | `#about` | Marketing anchor |
| Log in | `/login` | Candidate login entry |
| Sign up | `/register` canonical | Candidate registration entry |

#### Candidate Navigation

| Item | Destination |
| --- | --- |
| Dashboard | `/candidate/dashboard` |
| Jobs | `/jobs` |
| My Applications | `/candidate/my-applications` |
| Profile | `/candidate/profile` |

#### Internal Navigation

| Item | Destination |
| --- | --- |
| Dashboard | `/hr/dashboard` |
| Jobs | `/jobs` internal variant |
| Candidates | `/hr/candidates` |
| Applications | `/hr/applications` |
| Interviews | `/hr/interviews` |
| Post New Job CTA | `/hr/jobs/create` |

### Screen Component Inventory

| Screen | Tables | Forms | Buttons / Actions | Modals / Popovers |
| --- | --- | --- | --- | --- |
| `LandingPageScreen` | None | None | `Apply Now`, auth links | None |
| `CandidateLoginScreen` | None | Login form | `Sign In`, toggle password | None |
| `CandidateRegisterScreen` | None | Multi-step registration form | `Next Step`, `Previous`, `Create Account`, upload resume | None |
| `InternalLoginScreen` | None | Login form | `Secure Access`, toggle password, preview jobs | None |
| `JobListingCandidateScreen` | None | Search/filter UI | `Clear All`, filter toggles, add/remove skill, `Enable Notifications`, `Apply Now` | None |
| `JobManagementScreen` | Jobs table | Filter controls, edit form | `Post New Job`, `View Applications`, `Edit`, `Delete`, pagination | `Edit Job` modal |
| `JobDetailScreen` | Recent applications table | None | `Edit Job`, `View Applications`, `Close Posting`, `View All`, `Copy Shareable Link` | None |
| `DashboardCandidateScreen` | None | None | `Join Meeting`, `View All Listings`, recommended job CTAs | None |
| `MyApplicationScreen` | Applications table | Filter/search controls | `Accept Offer`, `View Detail`, `Withdraw` | None |
| `CandidateProfileAndCVManagementScreen` | Experience timeline | Profile edit fields, skills input, resume upload, experience composer | `Save Changes`, `Edit Profile`, `Add Skill`, `View Resume`, `Download Resume`, `Add Entry`, `Cancel` | Inline skill/experience composers |
| `HrDashboardScreen` | Recent applications table | None | `Export Report`, `View All`, `Review Draft`, `View All Approvals`, `View Full Report` | None |
| `JobCreatingScreen` | None | Multi-step job creation form | `Save Draft`, `Continue`, `Back`, `Publish Job`, `Use Engineering L4 Template` | None |
| `CandidateListScreen` | Candidate table | Search/filter controls | `Import Candidates`, `Add Candidate`, `View Profile`, `Edit`, pagination | None |
| `CandidateApplicationScreen` | Application table | Search/filter controls | `Apply Filters`, `Send Email`, `View CV`, `More Options` | Email action dropdown |
| `JobInterviewListScreen` | Interview table | Search/filter controls | `Save`, `Export CSV`, `View details`, `Reschedule`, `Mark completed`, `Cancel`, floating `Add interview` | Row action popover |
| `InterviewScheduleScreen` | None | Scheduling configuration form | `View Profile`, slot selection, `Swap Interviewer`, `Confirm Schedule`, `Save as Draft` | None |

---

## Backend Inventory

### Controller and Endpoint Inventory

| Controller | Endpoint | Current Code Access | Business Capability |
| --- | --- | --- | --- |
| `AuthController` | `POST api/auth/login` | Public | Generic login |
| `AuthController` | `POST api/auth/candidate/login` | Public | Candidate login |
| `AuthController` | `POST api/auth/internal/login` | Public | Internal login |
| `CandidateController` | `POST api/candidates/register` | Public | Candidate registration |
| `CandidateController` | `POST api/candidate/register` | Public | Candidate registration alias |
| `CandidateController` | `GET api/candidate/profile` | Candidate | View own profile |
| `CandidateController` | `PUT api/candidate/profile` | Candidate | Update own profile |
| `CandidateController` | `PUT api/candidate/profile/skills` | Candidate | Update own skills |
| `CandidateController` | `POST api/candidate/profile/experience` | Candidate | Create own experience |
| `CandidateController` | `PUT api/candidate/profile/experience/{experienceId}` | Candidate | Update own experience |
| `CandidateController` | `DELETE api/candidate/profile/experience/{experienceId}` | Candidate | Delete own experience |
| `CandidateController` | `POST api/candidate/profile/resume` | Candidate | Upload own resume |
| `CandidateController` | `GET api/hr/candidates` | HR/Manager/SystemAdmin | View candidate list |
| `DashboardController` | `GET api/candidate/dashboard` | Candidate | View candidate dashboard |
| `DashboardController` | `GET api/hr/dashboard` | HR/Manager/SystemAdmin | View internal dashboard |
| `JobController` | `GET api/jobs` | Public | Search/list jobs |
| `JobController` | `GET api/jobs/filters` | Public | Get job filters |
| `JobController` | `GET api/jobs/{jobId}` | Public | View job detail |
| `JobController` | `GET api/jobs/{jobId}/statistics` | Public today | View job statistics |
| `JobController` | `PATCH api/jobs/{jobId}/status` | Public today | Change job status |
| `JobController` | `GET api/hr/jobs` | HR/Manager/SystemAdmin | View internal jobs |
| `JobController` | `POST api/hr/jobs` | HR/Manager/SystemAdmin | Create job |
| `JobController` | `PATCH api/hr/jobs/{jobId}` | HR/Manager/SystemAdmin | Edit job |
| `JobController` | `PATCH api/hr/jobs/{jobId}/status` | HR/Manager/SystemAdmin | Change internal job approval status |
| `JobController` | `DELETE api/hr/jobs/{jobId}` | HR/Manager/SystemAdmin | Delete job |
| `ApplicationController` | `GET api/jobs/{jobId}/applications` | Public today | View job applications |
| `ApplicationController` | `GET api/jobs/{jobId}/applications/recent` | Public today | View recent job applications |
| `ApplicationController` | `POST api/jobs/{jobId}/apply` | Candidate | Apply to job |
| `ApplicationController` | `GET api/candidate/applications` | Candidate | View own applications |
| `ApplicationController` | `POST api/candidate/applications/{applicationId}/withdraw` | Candidate | Withdraw own application |
| `ApplicationController` | `POST api/candidate/applications/{applicationId}/accept-offer` | Candidate | Accept own offer |
| `ApplicationController` | `GET api/hr/applications` | HR/Manager/SystemAdmin | View all applications |
| `ApplicationController` | `GET api/hr/applications/{applicationId}/cv` | HR/Manager/SystemAdmin | View CV |
| `ApplicationController` | `POST api/hr/applications/{applicationId}/send-email` | HR/Manager/SystemAdmin | Send application email |
| `InterviewController` | `GET api/hr/interviews` | HR/Manager/SystemAdmin | View interviews |
| `InterviewController` | `GET api/hr/interviews/schedule-data` | HR/Manager/SystemAdmin | Load schedule data |
| `InterviewController` | `POST api/hr/interviews` | HR/Manager/SystemAdmin | Create interview |
| `InterviewController` | `PATCH api/hr/interviews/{interviewId}/status` | HR/Manager/SystemAdmin | Update interview status |
| `InterviewController` | `DELETE api/hr/interviews/{interviewId}` | HR/Manager/SystemAdmin | Delete interview |
| `LookupController` | `GET api/departments` | Public | View departments lookup |
| `LookupController` | `GET api/skills` | Public | View skills lookup |

### Service Capability Inventory

| Service | Capability Summary |
| --- | --- |
| `AuthService` | Candidate/internal login routing by role |
| `CandidateService` | Registration, own profile management, HR candidate listing |
| `DashboardService` | Candidate dashboard and internal dashboard |
| `JobService` | Public job discovery, internal job management, funnel stats |
| `ApplicationService` | Apply, withdraw, accept offer, internal application review and communication |
| `InterviewService` | Interview list, schedule data, create/update/delete interview |

---

## Permission Matrix

Permissions use `resource:action`.

### Auth Permissions

| Permission | Description |
| --- | --- |
| `auth:login-candidate` | Access candidate authentication flow |
| `auth:login-internal` | Access internal authentication flow |

### Dashboard Permissions

| Permission | Description |
| --- | --- |
| `dashboard:view-own` | View candidate dashboard |
| `dashboard:view-internal` | View internal dashboard |
| `dashboard:export` | Export dashboard/report data |

### Job Permissions

| Permission | Description |
| --- | --- |
| `job:list` | View jobs list |
| `job:view` | View job detail |
| `job:filter` | Use job search/filter capabilities |
| `job:apply` | Apply to a job |
| `job:create` | Create job |
| `job:update` | Edit job content |
| `job:approve` | Approve or move job through approval/status gates |
| `job:delete` | Delete job |
| `job:view-statistics` | View job funnel/statistics |
| `job:view-applications` | View applications for a job |
| `job:view-recent-applications` | View recent applications for a job |
| `job:share` | Copy/share job link |
| `job:use-template` | Use job creation template |

### Candidate Permissions

| Permission | Description |
| --- | --- |
| `candidate:register` | Register candidate account |
| `candidate:view-own-profile` | View own profile |
| `candidate:update-own-profile` | Edit own profile |
| `candidate:update-own-skills` | Edit own skills |
| `candidate:create-own-experience` | Create own experience |
| `candidate:update-own-experience` | Update own experience |
| `candidate:delete-own-experience` | Delete own experience |
| `candidate:upload-own-resume` | Upload own resume |
| `candidate:view-list` | View internal candidate list |
| `candidate:view-detail` | View internal candidate detail/profile |
| `candidate:update` | Edit candidate internally |
| `candidate:create` | Add candidate internally |
| `candidate:import` | Import candidates in bulk |

### Application Permissions

| Permission | Description |
| --- | --- |
| `application:view-own` | View own applications |
| `application:withdraw-own` | Withdraw own application |
| `application:accept-offer-own` | Accept own offer |
| `application:view-all` | View all applications internally |
| `application:view-cv` | View candidate CV |
| `application:send-email` | Send candidate email |
| `application:approve` | Advance/approve application in internal workflow |
| `application:reject` | Reject application in internal workflow |

### Interview Permissions

| Permission | Description |
| --- | --- |
| `interview:view-all` | View interview list |
| `interview:view-schedule-data` | View interviewer/candidate scheduling data |
| `interview:create` | Schedule interview |
| `interview:update` | Edit or reschedule interview |
| `interview:approve` | Confirm/finalize schedule or status progression |
| `interview:delete` | Cancel/delete interview |
| `interview:export` | Export interview list |

### Lookup Permissions

| Permission | Description |
| --- | --- |
| `lookup:view-departments` | View departments lookup |
| `lookup:view-skills` | View skills lookup |

### System Permissions

| Permission | Description |
| --- | --- |
| `system:admin` | Full internal administrative override |

---

## Role Matrix

`Manager` inherits all `HR` permissions.  
`SystemAdmin` inherits all internal permissions.

| Permission | Candidate | HR | Manager | SystemAdmin |
| --- | --- | --- | --- | --- |
| `auth:login-candidate` | Yes | No | No | No |
| `auth:login-internal` | No | Yes | Yes | Yes |
| `dashboard:view-own` | Yes | No | No | No |
| `dashboard:view-internal` | No | Yes | Yes | Yes |
| `dashboard:export` | No | Yes | Yes | Yes |
| `job:list` | Yes | Yes | Yes | Yes |
| `job:view` | Yes | Yes | Yes | Yes |
| `job:filter` | Yes | Yes | Yes | Yes |
| `job:apply` | Yes | No | No | No |
| `job:create` | No | Yes | Yes | Yes |
| `job:update` | No | Yes | Yes | Yes |
| `job:approve` | No | Yes | Yes | Yes |
| `job:delete` | No | Yes | Yes | Yes |
| `job:view-statistics` | No | Yes | Yes | Yes |
| `job:view-applications` | No | Yes | Yes | Yes |
| `job:view-recent-applications` | No | Yes | Yes | Yes |
| `job:share` | Yes | Yes | Yes | Yes |
| `job:use-template` | No | Yes | Yes | Yes |
| `candidate:register` | Yes | No | No | No |
| `candidate:view-own-profile` | Yes | No | No | No |
| `candidate:update-own-profile` | Yes | No | No | No |
| `candidate:update-own-skills` | Yes | No | No | No |
| `candidate:create-own-experience` | Yes | No | No | No |
| `candidate:update-own-experience` | Yes | No | No | No |
| `candidate:delete-own-experience` | Yes | No | No | No |
| `candidate:upload-own-resume` | Yes | No | No | No |
| `candidate:view-list` | No | Yes | Yes | Yes |
| `candidate:view-detail` | No | Yes | Yes | Yes |
| `candidate:update` | No | Yes | Yes | Yes |
| `candidate:create` | No | Yes | Yes | Yes |
| `candidate:import` | No | Yes | Yes | Yes |
| `application:view-own` | Yes | No | No | No |
| `application:withdraw-own` | Yes | No | No | No |
| `application:accept-offer-own` | Yes | No | No | No |
| `application:view-all` | No | Yes | Yes | Yes |
| `application:view-cv` | No | Yes | Yes | Yes |
| `application:send-email` | No | Yes | Yes | Yes |
| `application:approve` | No | Yes | Yes | Yes |
| `application:reject` | No | Yes | Yes | Yes |
| `interview:view-all` | No | Yes | Yes | Yes |
| `interview:view-schedule-data` | No | Yes | Yes | Yes |
| `interview:create` | No | Yes | Yes | Yes |
| `interview:update` | No | Yes | Yes | Yes |
| `interview:approve` | No | Yes | Yes | Yes |
| `interview:delete` | No | Yes | Yes | Yes |
| `interview:export` | No | Yes | Yes | Yes |
| `lookup:view-departments` | Yes | Yes | Yes | Yes |
| `lookup:view-skills` | Yes | Yes | Yes | Yes |
| `system:admin` | No | No | No | Yes |

---

## Navigation Matrix

### Public Navigation

| Navigation Item | Destination | Visible Roles | Required Permission |
| --- | --- | --- | --- |
| Home | `/home` | All visitors | None |
| Careers | `#careers` | All visitors | None |
| About Us | `#about` | All visitors | None |
| Log in | `/login` | Unauthenticated candidate/public users | `auth:login-candidate` intent |
| Sign up | `/register` | Unauthenticated candidate/public users | `candidate:register` intent |

### Candidate Navigation

| Navigation Item | Destination | Visible Roles | Required Permission |
| --- | --- | --- | --- |
| Dashboard | `/candidate/dashboard` | Candidate | `dashboard:view-own` |
| Jobs | `/jobs` | Candidate | `job:list` |
| My Applications | `/candidate/my-applications` | Candidate | `application:view-own` |
| Profile | `/candidate/profile` | Candidate | `candidate:view-own-profile` |

### Internal Navigation

| Navigation Item | Destination | Visible Roles | Required Permission |
| --- | --- | --- | --- |
| Dashboard | `/hr/dashboard` | HR, Manager, SystemAdmin | `dashboard:view-internal` |
| Jobs | `/jobs` internal variant | HR, Manager, SystemAdmin | `job:list` |
| Candidates | `/hr/candidates` | HR, Manager, SystemAdmin | `candidate:view-list` |
| Applications | `/hr/applications` | HR, Manager, SystemAdmin | `application:view-all` |
| Interviews | `/hr/interviews` | HR, Manager, SystemAdmin | `interview:view-all` |
| Post New Job CTA | `/hr/jobs/create` | HR, Manager, SystemAdmin | `job:create` |

### Role Navigation Summary

| Role | Visible Navigation Set |
| --- | --- |
| Candidate | Candidate navigation only |
| HR | Internal navigation only |
| Manager | Same as HR |
| SystemAdmin | Same as HR plus full override over all internal navigation |

---

## Route Protection Matrix

| Route | Route Type | Required Permission(s) | Allowed Roles | Protection Notes |
| --- | --- | --- | --- | --- |
| `/home` | Public | None | All | No auth required |
| `/login` | PublicOnly | `auth:login-candidate` intent | Unauthenticated users | Redirect authenticated users away |
| `/register` | PublicOnly | `candidate:register` intent | Unauthenticated users | Redirect authenticated users away |
| `/internal/login` | PublicOnly | `auth:login-internal` intent | Unauthenticated internal users | Redirect authenticated users away |
| `/jobs` candidate variant | Shared | `job:list` | Candidate, HR, Manager, SystemAdmin | Route content varies by permission set |
| `/jobs/:jobId` | Shared | `job:view` | Candidate, HR, Manager, SystemAdmin | Internal actions additionally require internal permissions |
| `/candidate/dashboard` | Protected | `dashboard:view-own` | Candidate | Candidate-only route |
| `/candidate/my-applications` | Protected | `application:view-own` | Candidate | Candidate-only route |
| `/candidate/profile/*` | Protected | `candidate:view-own-profile` | Candidate | Candidate-only route |
| `/hr/dashboard` | Protected | `dashboard:view-internal` | HR, Manager, SystemAdmin | Internal-only route |
| `/hr/jobs/create` | Protected | `job:create` | HR, Manager, SystemAdmin | Internal-only route |
| `/hr/candidates` | Protected | `candidate:view-list` | HR, Manager, SystemAdmin | Internal-only route |
| `/hr/applications` | Protected | `application:view-all` | HR, Manager, SystemAdmin | Internal-only route |
| `/hr/interviews` | Protected | `interview:view-all` | HR, Manager, SystemAdmin | Internal-only route |
| `/hr/interviews/schedule` | Protected | `interview:view-schedule-data`, `interview:create` | HR, Manager, SystemAdmin | Internal-only route |

---

## Screen Visibility Matrix

Definitions:

- `Access`: may enter the route/screen
- `View`: may see screen data/content
- `Edit`: may edit mutable content on the screen
- `Approve`: may perform approval/finalization actions
- `Delete`: may remove/cancel entities from the screen

| Screen | Route | Access Roles | View Roles | Edit Roles | Approve Roles | Delete Roles |
| --- | --- | --- | --- | --- | --- | --- |
| Landing Page | `/home` | All | All | None | None | None |
| Candidate Login | `/login` | Unauthenticated users | Unauthenticated users | None | None | None |
| Candidate Register | `/register` | Unauthenticated users | Unauthenticated users | Candidate intent only | None | None |
| Internal Login | `/internal/login` | Unauthenticated internal users | Unauthenticated internal users | None | None | None |
| Job Listing | `/jobs` candidate variant | Candidate, HR, Manager, SystemAdmin | Candidate, HR, Manager, SystemAdmin | None | None | None |
| Job Management | `/jobs` internal variant | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin |
| Job Detail | `/jobs/:jobId` | Candidate, HR, Manager, SystemAdmin | Candidate, HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin |
| Candidate Dashboard | `/candidate/dashboard` | Candidate | Candidate | None | None | None |
| My Applications | `/candidate/my-applications` | Candidate | Candidate | Candidate on own records | Candidate for own offer acceptance where applicable | Candidate for own withdraw action only |
| Candidate Profile & CV | `/candidate/profile/*` | Candidate | Candidate | Candidate | None | Candidate for own experience entries |
| HR Dashboard | `/hr/dashboard` | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | None | HR, Manager, SystemAdmin for draft/approval actions | None |
| Job Creating | `/hr/jobs/create` | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin on publish/submit | None |
| Candidate List | `/hr/candidates` | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | None | None |
| Candidate Applications | `/hr/applications` | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin for email actions | HR, Manager, SystemAdmin if workflow approval/rejection is later added | None |
| Interview List | `/hr/interviews` | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin |
| Interview Schedule | `/hr/interviews/schedule` | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | HR, Manager, SystemAdmin | None |

### Screen Classification

| Classification | Screens |
| --- | --- |
| Shared/Public | `LandingPageScreen`, `JobListingCandidateScreen`, `JobDetailScreen` |
| Candidate-only | `CandidateLoginScreen`, `CandidateRegisterScreen`, `DashboardCandidateScreen`, `MyApplicationScreen`, `CandidateProfileAndCVManagementScreen` |
| Internal-only | `InternalLoginScreen`, `HrDashboardScreen`, `JobCreatingScreen`, `CandidateListScreen`, `CandidateApplicationScreen`, `JobInterviewListScreen`, `InterviewScheduleScreen`, internal variant of `JobManagementScreen` |
| Manager-only | None currently implemented |
| SystemAdmin-only | None currently implemented |

---

## Action Visibility Matrix

### Candidate/Public Actions

| Action | Screen | Visible Roles | Required Permission |
| --- | --- | --- | --- |
| Sign In | Candidate Login | Candidate/public unauthenticated users | `auth:login-candidate` |
| Create Account | Candidate Register | Candidate/public unauthenticated users | `candidate:register` |
| Upload Resume during Registration | Candidate Register | Candidate/public unauthenticated users | `candidate:upload-own-resume` intent |
| Search Jobs | Job Listing | Candidate, HR, Manager, SystemAdmin | `job:list` |
| Filter Jobs | Job Listing | Candidate, HR, Manager, SystemAdmin | `job:filter` |
| Add Skill Filter | Job Listing | Candidate, HR, Manager, SystemAdmin | `job:filter` |
| Clear All Filters | Job Listing | Candidate, HR, Manager, SystemAdmin | `job:filter` |
| Apply Button | Job Listing / Job Detail / Candidate Dashboard CTAs | Candidate | `job:apply` |
| Enable Notifications | Job Listing | Candidate | `job:list` |
| Join Meeting | Candidate Dashboard | Candidate | `dashboard:view-own` |
| View All Listings | Candidate Dashboard | Candidate | `job:list` |
| Accept Offer Button | My Applications | Candidate | `application:accept-offer-own` |
| Withdraw Button | My Applications | Candidate | `application:withdraw-own` |
| View Detail Button | My Applications | Candidate | `application:view-own` |
| Edit Profile Button | Candidate Profile | Candidate | `candidate:update-own-profile` |
| Save Changes Button | Candidate Profile | Candidate | `candidate:update-own-profile` |
| Add Skill Button | Candidate Profile | Candidate | `candidate:update-own-skills` |
| Remove Skill Action | Candidate Profile | Candidate | `candidate:update-own-skills` |
| Add Entry Button | Candidate Profile | Candidate | `candidate:create-own-experience` |
| Cancel Entry Composer | Candidate Profile | Candidate | `candidate:create-own-experience` |
| Add Entry Confirm | Candidate Profile | Candidate | `candidate:create-own-experience` |
| Upload CV Button | Candidate Profile | Candidate | `candidate:upload-own-resume` |
| Download Resume Button | Candidate Profile | Candidate | `candidate:upload-own-resume` |
| View Resume Button | Candidate Profile | Candidate | `candidate:view-own-profile` |
| Copy Shareable Link | Job Detail | Candidate, HR, Manager, SystemAdmin | `job:share` |

### Internal Job Actions

| Action | Screen | Visible Roles | Required Permission |
| --- | --- | --- | --- |
| Post New Job CTA | Side Navigation / Job Management | HR, Manager, SystemAdmin | `job:create` |
| Publish Job | Job Creating | HR, Manager, SystemAdmin | `job:create` |
| Save Draft Job | Job Creating | HR, Manager, SystemAdmin | `job:create` |
| Continue Job Creation Steps | Job Creating | HR, Manager, SystemAdmin | `job:create` |
| Back in Job Creation | Job Creating | HR, Manager, SystemAdmin | `job:create` |
| Use Engineering Template | Job Creating | HR, Manager, SystemAdmin | `job:use-template` |
| Edit Job Button | Job Management / Job Detail | HR, Manager, SystemAdmin | `job:update` |
| Save Job Modal | Job Management modal | HR, Manager, SystemAdmin | `job:update` |
| Delete Job Button | Job Management | HR, Manager, SystemAdmin | `job:delete` |
| Close Posting Button | Job Detail | HR, Manager, SystemAdmin | `job:approve` |
| Review Draft Button | HR Dashboard | HR, Manager, SystemAdmin | `job:approve` |
| View Applications Button | Job Management / Job Detail | HR, Manager, SystemAdmin | `job:view-applications` |
| View All Recent Applications | HR Dashboard / Job Detail | HR, Manager, SystemAdmin | `job:view-applications` |

### Internal Candidate Actions

| Action | Screen | Visible Roles | Required Permission |
| --- | --- | --- | --- |
| Import Candidates Button | Candidate List | HR, Manager, SystemAdmin | `candidate:import` |
| Add Candidate Button | Candidate List | HR, Manager, SystemAdmin | `candidate:create` |
| View Profile Button | Candidate List / Interview Schedule | HR, Manager, SystemAdmin | `candidate:view-detail` |
| Edit Candidate Button | Candidate List | HR, Manager, SystemAdmin | `candidate:update` |

### Internal Application Actions

| Action | Screen | Visible Roles | Required Permission |
| --- | --- | --- | --- |
| Apply Filters | Candidate Applications | HR, Manager, SystemAdmin | `application:view-all` |
| View CV Button | Candidate Applications | HR, Manager, SystemAdmin | `application:view-cv` |
| Send Email Button | Candidate Applications | HR, Manager, SystemAdmin | `application:send-email` |
| Interview Invitation Email | Candidate Applications | HR, Manager, SystemAdmin | `application:send-email` |
| Job Offer Email | Candidate Applications | HR, Manager, SystemAdmin | `application:send-email` |
| Rejection Email | Candidate Applications | HR, Manager, SystemAdmin | `application:send-email` |
| Approve Application | Future internal workflow | HR, Manager, SystemAdmin | `application:approve` |
| Reject Application | Future internal workflow | HR, Manager, SystemAdmin | `application:reject` |

### Internal Interview Actions

| Action | Screen | Visible Roles | Required Permission |
| --- | --- | --- | --- |
| Export CSV Button | Interview List | HR, Manager, SystemAdmin | `interview:export` |
| Save Interview Overrides | Interview List | HR, Manager, SystemAdmin | `interview:update` |
| View Interview Details | Interview List | HR, Manager, SystemAdmin | `interview:view-all` |
| Reschedule Interview | Interview List | HR, Manager, SystemAdmin | `interview:update` |
| Mark Completed | Interview List | HR, Manager, SystemAdmin | `interview:approve` |
| Cancel Interview | Interview List | HR, Manager, SystemAdmin | `interview:delete` |
| Add Interview Floating Button | Interview List | HR, Manager, SystemAdmin | `interview:create` |
| Swap Interviewer | Interview Schedule | HR, Manager, SystemAdmin | `interview:view-schedule-data` |
| Select Time Slot | Interview Schedule | HR, Manager, SystemAdmin | `interview:create` |
| Confirm Schedule Button | Interview Schedule | HR, Manager, SystemAdmin | `interview:approve` |
| Save as Draft Button | Interview Schedule | HR, Manager, SystemAdmin | `interview:update` |

### Internal Dashboard / Reporting Actions

| Action | Screen | Visible Roles | Required Permission |
| --- | --- | --- | --- |
| Export Report | HR Dashboard | HR, Manager, SystemAdmin | `dashboard:export` |
| View Full Report | HR Dashboard | HR, Manager, SystemAdmin | `dashboard:view-internal` |
| View All Approvals | HR Dashboard | HR, Manager, SystemAdmin | `dashboard:view-internal` |

---

## Page Capability Matrix

This matrix answers: for every page, who can access, view, edit, approve, and delete.

| Page | Candidate | HR | Manager | SystemAdmin | Access Permission | View Permission | Edit Permission | Approve Permission | Delete Permission |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Landing Page | Access/View | Access/View | Access/View | Access/View | None | None | None | None | None |
| Candidate Login | Access/View | No | No | No | `auth:login-candidate` intent | `auth:login-candidate` intent | None | None | None |
| Candidate Register | Access/View/Edit | No | No | No | `candidate:register` | `candidate:register` | `candidate:register` | None | None |
| Internal Login | No | Access/View | Access/View | Access/View | `auth:login-internal` | `auth:login-internal` | None | None | None |
| Job Listing | Access/View | Access/View | Access/View | Access/View | `job:list` | `job:list` | None | None | None |
| Job Detail | Access/View | Access/View/Edit/Approve/Delete | Access/View/Edit/Approve/Delete | Access/View/Edit/Approve/Delete | `job:view` | `job:view` | `job:update` | `job:approve` | `job:delete` |
| Candidate Dashboard | Access/View | No | No | No | `dashboard:view-own` | `dashboard:view-own` | None | None | None |
| My Applications | Access/View/Edit | No | No | No | `application:view-own` | `application:view-own` | `application:withdraw-own`, `application:accept-offer-own` | `application:accept-offer-own` | `application:withdraw-own` |
| Candidate Profile & CV | Access/View/Edit/Delete-own-experience | No | No | No | `candidate:view-own-profile` | `candidate:view-own-profile` | `candidate:update-own-profile`, `candidate:update-own-skills`, `candidate:create-own-experience`, `candidate:update-own-experience`, `candidate:upload-own-resume` | None | `candidate:delete-own-experience` |
| HR Dashboard | No | Access/View/Approve | Access/View/Approve | Access/View/Approve | `dashboard:view-internal` | `dashboard:view-internal` | None | `job:approve` | None |
| Job Management | No | Access/View/Edit/Approve/Delete | Access/View/Edit/Approve/Delete | Access/View/Edit/Approve/Delete | `job:list` | `job:list` | `job:update` | `job:approve` | `job:delete` |
| Job Creating | No | Access/View/Edit/Approve | Access/View/Edit/Approve | Access/View/Edit/Approve | `job:create` | `job:create` | `job:create` | `job:approve` | None |
| Candidate List | No | Access/View/Edit | Access/View/Edit | Access/View/Edit | `candidate:view-list` | `candidate:view-list` | `candidate:update`, `candidate:create`, `candidate:import` | None | None |
| Candidate Applications | No | Access/View/Edit/Approve | Access/View/Edit/Approve | Access/View/Edit/Approve | `application:view-all` | `application:view-all` | `application:send-email` | `application:approve`, `application:reject` | None |
| Interview List | No | Access/View/Edit/Approve/Delete | Access/View/Edit/Approve/Delete | Access/View/Edit/Approve/Delete | `interview:view-all` | `interview:view-all` | `interview:update` | `interview:approve` | `interview:delete` |
| Interview Schedule | No | Access/View/Edit/Approve | Access/View/Edit/Approve | Access/View/Edit/Approve | `interview:view-schedule-data`, `interview:create` | `interview:view-schedule-data` | `interview:update`, `interview:create` | `interview:approve` | None |

---

## Backend Authorization Mapping

Recommended controller/endpoint permission mapping:

| Endpoint | Recommended Permission |
| --- | --- |
| `POST api/auth/candidate/login` | `auth:login-candidate` |
| `POST api/auth/internal/login` | `auth:login-internal` |
| `POST api/candidates/register` | `candidate:register` |
| `GET api/candidate/profile` | `candidate:view-own-profile` |
| `PUT api/candidate/profile` | `candidate:update-own-profile` |
| `PUT api/candidate/profile/skills` | `candidate:update-own-skills` |
| `POST api/candidate/profile/experience` | `candidate:create-own-experience` |
| `PUT api/candidate/profile/experience/{experienceId}` | `candidate:update-own-experience` |
| `DELETE api/candidate/profile/experience/{experienceId}` | `candidate:delete-own-experience` |
| `POST api/candidate/profile/resume` | `candidate:upload-own-resume` |
| `GET api/hr/candidates` | `candidate:view-list` |
| `GET api/candidate/dashboard` | `dashboard:view-own` |
| `GET api/hr/dashboard` | `dashboard:view-internal` |
| `GET api/jobs` | `job:list` |
| `GET api/jobs/filters` | `job:filter` |
| `GET api/jobs/{jobId}` | `job:view` |
| `GET api/jobs/{jobId}/statistics` | `job:view-statistics` |
| `PATCH api/jobs/{jobId}/status` | `job:approve` |
| `GET api/hr/jobs` | `job:list` |
| `POST api/hr/jobs` | `job:create` |
| `PATCH api/hr/jobs/{jobId}` | `job:update` |
| `PATCH api/hr/jobs/{jobId}/status` | `job:approve` |
| `DELETE api/hr/jobs/{jobId}` | `job:delete` |
| `GET api/jobs/{jobId}/applications` | `job:view-applications` |
| `GET api/jobs/{jobId}/applications/recent` | `job:view-recent-applications` |
| `POST api/jobs/{jobId}/apply` | `job:apply` |
| `GET api/candidate/applications` | `application:view-own` |
| `POST api/candidate/applications/{applicationId}/withdraw` | `application:withdraw-own` |
| `POST api/candidate/applications/{applicationId}/accept-offer` | `application:accept-offer-own` |
| `GET api/hr/applications` | `application:view-all` |
| `GET api/hr/applications/{applicationId}/cv` | `application:view-cv` |
| `POST api/hr/applications/{applicationId}/send-email` | `application:send-email` |
| `GET api/hr/interviews` | `interview:view-all` |
| `GET api/hr/interviews/schedule-data` | `interview:view-schedule-data` |
| `POST api/hr/interviews` | `interview:create` |
| `PATCH api/hr/interviews/{interviewId}/status` | `interview:approve` |
| `DELETE api/hr/interviews/{interviewId}` | `interview:delete` |
| `GET api/departments` | `lookup:view-departments` |
| `GET api/skills` | `lookup:view-skills` |

---

## Frontend Enforcement Mapping

### Route Guards

- Candidate routes:
  - `/candidate/dashboard` -> `dashboard:view-own`
  - `/candidate/my-applications` -> `application:view-own`
  - `/candidate/profile/*` -> `candidate:view-own-profile`

- Internal routes:
  - `/hr/dashboard` -> `dashboard:view-internal`
  - `/hr/jobs/create` -> `job:create`
  - `/hr/candidates` -> `candidate:view-list`
  - `/hr/applications` -> `application:view-all`
  - `/hr/interviews` -> `interview:view-all`
  - `/hr/interviews/schedule` -> `interview:view-schedule-data` + `interview:create`

### Menu Rendering

- Candidate menu renders only if candidate permissions exist.
- Internal menu renders only if internal permissions exist.
- `Post New Job` CTA renders only with `job:create`.

### Button Rendering

- Never infer button visibility from route alone.
- Every actionable button should map to exactly one required permission.
- Composite workflows may require multiple permissions:
  - Example: reschedule interview may need `interview:update`
  - Example: entering interview scheduling screen may need both `interview:view-schedule-data` and `interview:create`

---

## Authorization Gaps To Address In Future Refactor

| Area | Current State | Target RBAC |
| --- | --- | --- |
| Job statistics endpoint | Public | `job:view-statistics` |
| Job applications endpoints | Public | `job:view-applications`, `job:view-recent-applications` |
| Public job status patch | Public | `job:approve` |
| Shared job detail actions | Visible on shared route | Gate each action individually by permission |
| Application approval/rejection | Not explicit in current UI | Introduce explicit `application:approve` and `application:reject` actions if workflow expands |
| Manager-only UI | Not implemented | Keep Manager = HR inheritance until new business rule appears |
| SystemAdmin-only UI | Not implemented | Use full override role for backend and hidden admin tools when added |

---

## Final Role Positioning

| Role | Final Positioning |
| --- | --- |
| `Candidate` | Public recruiting discovery + self-service profile/application management |
| `HR` | Full internal recruiting operations |
| `Manager` | All HR permissions, no unique workflow today |
| `SystemAdmin` | Full internal platform visibility and override authority |


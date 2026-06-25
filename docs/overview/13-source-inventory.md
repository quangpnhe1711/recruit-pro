# 13. Source Inventory

## Controllers

- `AuthController`
- `ApplicationController`
- `CandidateController`
- `DashboardController`
- `JobController`
- `InterviewController`
- `NotificationController`
- `LookupController`
- `OfferController`
- `ManagerAnalyticsController`
- `CopilotController`
- `SemanticDiscoveryController`
- `ResumeController`

## Services

- `AuthService`
- `CandidateService`
- `JobService`
- `ApplicationService`
- `InterviewService`
- `DashboardService`
- `OfferService`
- `NotificationService`
- `NotificationEventService`
- `ManagerAnalyticsService`
- `CopilotService`
- `SemanticDiscoveryService`
- `ApplicationSemanticScoringService`

## Repositories

- `UserRepository`
- `JobRepository`
- `SkillRepository`
- `InterviewRepository`
- `OfferRepository`
- `NotificationRepository`
- `CopilotRepository`
- `CandidateProfileRepository`
- `ApplicationRepository`

## Entities

- `User`
- `Role`
- `Permission`
- `RolePermission`
- `UserRole`
- `RefreshToken`
- `Job`
- `JobSkill`
- `Department`
- `Skill`
- `CandidateProfile`
- `CandidateSkill`
- `CandidateProject`
- `CandidateResume`
- `CandidateProfileSection`
- `CandidateProfileSectionItem`
- `Application`
- `Interview`
- `Notification`
- `CopilotConversation`
- `CopilotMessage`
- `CopilotRankingSession`
- `CopilotRankingResult`
- `CopilotSavedRule`
- `CopilotCandidateTag`
- `SystemLog`

## DTOs

The source contains extensive request and response DTOs across:

- authentication
- jobs
- candidates
- applications
- interviews
- offers
- notifications
- copilot
- semantic discovery
- dashboard

## Background Workers

- `SemanticScoringBackgroundService`

## Configurations

- JWT settings
- MinIO settings
- AI provider settings
- validation and middleware setup

## Utilities

- format helpers
- storage helpers
- protected file helpers
- application/job presentation helpers
- resume link helpers
- pagination meta helpers

## Providers

- AI resume parser provider
- AI embedding provider
- AI copilot provider
- AI-compatible API helper
- file storage service
- email service


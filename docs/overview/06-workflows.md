# 06. Workflows

## Candidate Registration

```mermaid
sequenceDiagram
    participant C as Candidate
    participant FE as Frontend
    participant API as API
    participant APP as CandidateService
    participant DB as Database
    C->>FE: Fill registration form
    FE->>API: POST register
    API->>APP: RegisterAsync
    APP->>DB: Create user/profile
    DB-->>APP: Saved
    APP-->>API: Registration result
    API-->>FE: Response
```

## Resume Upload

```mermaid
sequenceDiagram
    participant C as Candidate
    participant FE as Frontend
    participant API as API
    participant APP as CandidateService
    participant FS as File Storage
    participant AI as Resume Parser
    C->>FE: Upload resume
    FE->>API: POST resume
    API->>APP: Upload/parse flow
    APP->>FS: Store file
    APP->>AI: Extract/parse text
    AI-->>APP: Parsed structured data or failure
    APP-->>API: Parse result
    API-->>FE: Preview or error
```

## Job Application

```mermaid
sequenceDiagram
    participant C as Candidate
    participant FE as Frontend
    participant API as API
    participant APP as ApplicationService
    participant DB as Database
    C->>FE: Apply job
    FE->>API: POST apply
    API->>APP: Create application
    APP->>DB: Save application
    DB-->>APP: Saved
    APP-->>API: Result
    API-->>FE: Success
```

## Application Review

- HR opens application list
- HR reviews candidate profile and CV
- HR changes status or sends email
- System records the action and may trigger notification

## Interview Process

- HR schedules interview from application
- Interview status updates follow the workflow
- Interview data appears in dashboard and list screens

## Offer Process

- HR creates or sends offer
- Candidate can accept offer where allowed
- Offer records are linked to application workflow

## Notification Flow

- A business action creates notification record
- SignalR hub can push realtime updates
- Candidate or internal user sees notification list/count

## Current AI Flow

```mermaid
sequenceDiagram
    participant UI as Frontend
    participant API as API
    participant APP as Application Layer
    participant AI as AI Provider
    participant DB as Database
    UI->>API: Trigger AI-related action
    API->>APP: Orchestrate workflow
    APP->>AI: Parse / embed / rank
    AI-->>APP: Result
    APP->>DB: Persist output
    DB-->>APP: Saved
    APP-->>API: Response
```


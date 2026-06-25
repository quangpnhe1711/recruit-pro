# 04. Database

## Database Engine

- PostgreSQL

## Documentation Scope

This document describes tables identifiable from the source code, domain entities, migrations, and API contracts.

## Core Tables

### users

Purpose:

- Stores system accounts for candidate and internal users

Important fields:

- identity, credentials, status, profile basics

Frequently queried:

- email, id, role mappings, status

### roles

Purpose:

- Stores role names

### permissions

Purpose:

- Stores permission definitions

### user_roles

Purpose:

- Many-to-many user-role assignment

### role_permissions

Purpose:

- Many-to-many role-permission assignment

### candidate_profiles

Purpose:

- Candidate normalized profile

Important fields:

- user link, bio, experience, education, resume info

Potential AI-related fields:

- resume-derived structured narrative

### candidate_profile_sections

Purpose:

- Flexible candidate profile sections

### candidate_profile_section_items

Purpose:

- Items inside flexible profile sections

### candidate_skills

Purpose:

- Candidate skill assignments

### candidate_projects

Purpose:

- Candidate projects

### candidate_resumes

Purpose:

- Resume file records and parse state

### jobs

Purpose:

- Job postings

Frequently queried:

- status, department, location, work mode, employment type

### job_skills

Purpose:

- Skills required or preferred for a job

### departments

Purpose:

- Department lookup

### skills

Purpose:

- Skill lookup

### applications

Purpose:

- Candidate job applications

Potential AI-related fields:

- rule score, semantic score, final score, score status

### interviews

Purpose:

- Interview scheduling records

### notifications

Purpose:

- In-app notification records

### offers and offer-related tables

Purpose:

- Offer templates and candidate offer details

### copilot_conversations

Purpose:

- One HR job-scoped copilot conversation

### copilot_messages

Purpose:

- Conversation history

### copilot_ranking_sessions

Purpose:

- One ranking execution for one prompt

### copilot_ranking_results

Purpose:

- Per-candidate ranking output

### copilot_saved_rules

Purpose:

- Saved HR screening rules

### copilot_candidate_tags

Purpose:

- AI or HR-generated labels for candidates

## Legacy Compatibility

The source shows both legacy structures and new flexible structures for candidate profile data. The system currently preserves backward compatibility.

## Frequently Queried Fields

- user email
- job status
- application status
- candidate profile by user id
- job by id
- interview by application id
- notification by user id
- copilot ranking by job and conversation


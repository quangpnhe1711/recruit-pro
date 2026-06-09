# Forbidden Patterns

Do not create:

- HrRepository
- AdminRepository
- CommonRepository
- UtilityService

Do not:

- Call SaveChanges inside Repository
- Return Entity from API
- Add Include blindly
- Put business logic in Controller

Use:

- DTO Projection
- Application Service
- UnitOfWork
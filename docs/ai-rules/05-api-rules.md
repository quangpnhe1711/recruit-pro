# API Rules

Never return Entity directly.

Always return DTO.

Response format:

{
  data,
  message,
  errors
}

Validation:

- FluentValidation
- Application Layer

Controllers should remain thin.
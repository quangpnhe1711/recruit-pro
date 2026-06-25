# 11. Infrastructure

## Docker

The repository contains:

- `Dockerfile`
- `docker-compose.yml`

## Storage

- File storage abstraction exists
- Infrastructure service includes MinIO-based file storage implementation

## Database

- PostgreSQL
- EF Core migrations present

## Caching

Confirmed cache-like components:

- in-memory embedding cache
- in-memory semantic processing queue

## Configuration

Confirmed configuration areas:

- JWT settings
- AI provider settings
- MinIO settings

## Environment Variables

Frontend:

- `VITE_API_BASE_URL`

Backend:

- app settings files for development and production

## Deployment

- Frontend can be deployed to Vercel
- Backend can run as ASP.NET Core API
- Static asset and proxy configuration exists in frontend repo


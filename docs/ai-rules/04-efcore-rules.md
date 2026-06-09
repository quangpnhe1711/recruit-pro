# EF Core Rules

Always:

- AsNoTracking for read queries
- Pagination for list endpoints
- Projection before materialization

Prefer:

.Select()

instead of

.Include()

when returning DTOs

Do not:

- Load entire object graphs
- Use Include blindly
- Call SaveChanges inside Repository

Transactions:

Repository:
- Add
- Update
- Remove

UnitOfWork:
- SaveChangesAsync
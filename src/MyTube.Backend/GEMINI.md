# MyTube Backend Engineering Directives

When modifying, adding, or reviewing files in `MyTube.Backend`:

1. **Follow the `dotnet-backend-best-practices` skill**:
   - Keep Domain independent of EF Core, ASP.NET Core, or Infrastructure.
   - Use CQRS with MediatR for use cases (separate Commands and Queries).
   - Use FluentValidation in MediatR pipeline behaviors.
   - Never return EF Core entities or `IQueryable` directly from API endpoints.
   - Propagate `CancellationToken` through all asynchronous paths.
   - Use `.AsNoTracking()` and `.Select()` DTO projections for read queries.
   - Never use `.Result` or `.Wait()`.
   - Never run parallel operations against the same `DbContext` instance.
   - Author unit/integration tests with xUnit and FluentAssertions.

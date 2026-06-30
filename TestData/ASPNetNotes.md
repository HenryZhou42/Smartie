# ASP.NET Core Notes

**Sample document for Smartie Knowledge Base** — use semantic search to find topics below.

## Middleware pipeline

ASP.NET Core builds a request pipeline from registered middleware. Order matters: exception handling first, then HTTPS redirection, static files, routing, authentication, authorization, and endpoints.

## Dependency injection

Services register in `Program.cs` or extension methods. Lifetimes:

- **Singleton** — one instance for the app lifetime
- **Scoped** — one instance per request (ideal for DbContext)
- **Transient** — new instance every time

## Minimal APIs

Map routes with `app.MapGet`, `MapPost`, etc. Inject services as handler parameters. Return `Results.Ok`, `Results.NotFound`, or typed DTOs.

## EF Core tips

- Use migrations for schema changes
- Prefer async APIs (`ToListAsync`, `SaveChangesAsync`)
- SQLite works well for local-first desktop apps

## Smartie connection

Smartie uses the same patterns: Clean Architecture, local SQLite, minimal API endpoints, and Semantic Kernel for AI providers.

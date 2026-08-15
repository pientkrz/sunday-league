# Sunday League

An MVP football-league manager with a React 19/Vite admin UI and an ASP.NET Core 10 minimal API.

## MVP scope

- Configure divisions, their hierarchy and team allocation.
- Enter fixtures and results; standings calculate automatically.
- Create a random matchday round, with a bye for odd-sized divisions.
- Publish standings and fixtures anonymously while protecting board actions behind login and scoped permissions.

## Run locally

```powershell
cd src/sunday-league-client
npm run dev

# In another terminal
dotnet run --project src/SundayLeague.Api --launch-profile https
```

The Vite development server proxies `/api` requests to `https://localhost:7134`. The dashboard and fixtures use the live public API. The Board Portal supports sign-in, accepting an invitation, scoped result entry, Owner invitations, league publishing/rules, team additions, league hierarchy ordering, and random or manual complete-round scheduling. The API is persistent: it uses SQLite locally and automatically uses PostgreSQL when `ConnectionStrings__DefaultConnection` begins with `Host=`. Schema changes are applied through EF Core migrations at startup rather than by creating an unmanaged database schema.

## Database migrations

The repository pins the EF Core command-line tool. Restore it once after cloning, then generate migrations whenever the persistence model changes:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add DescriptiveName --project src/SundayLeague.Api --startup-project src/SundayLeague.Api --output-dir Data/Migrations
```

Review generated migrations in pull requests. Back up a production database and run migrations as part of the deployment process before rolling out a new API version.

## Secure board setup

Public visitors use `/api/public/*` and never need an account. Board accounts are invite-only: there is intentionally no open registration endpoint.

Create the first Owner through user secrets or environment variables; do not put passwords in `appsettings.json` or commit them.

```powershell
dotnet user-secrets init --project src/SundayLeague.Api
dotnet user-secrets set "Bootstrap:OwnerEmail" "owner@example.com" --project src/SundayLeague.Api
dotnet user-secrets set "Bootstrap:OwnerPassword" "A-long-unique-password!42" --project src/SundayLeague.Api
```

The first start creates the Owner. An Owner can then create one-time, hashed invitations for Competition Admins, Results Editors, and Viewers. Results Editors are restricted to the leagues assigned to them and cannot change league configuration.

For production, run React and the API under the same HTTPS site, keep the Data Protection key ring outside the app directory with OS-level access controls (or a managed key store), configure PostgreSQL, and set `Invitations__ExposeCodes=false` so invitation tokens are delivered only by the email service.

## Tests

```powershell
dotnet restore src/SundayLeague.Api.Tests/SundayLeague.Api.Tests.csproj --configfile NuGet.Config
dotnet test src/SundayLeague.Api.Tests/SundayLeague.Api.Tests.csproj --no-restore
```

The integration tests verify anonymous public standings, blocked anonymous mutations, the boundary between a Results Editor's score-entry permission and configuration permission, plus Owner-only random and manual round scheduling.


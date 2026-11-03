# CLAUDE.md — stonetopSteading

A web app for tracking the Stonetop steading between TTRPG sessions. The GM edits the steading; players view a read-only, scrubbed version through a share link.

Repo: https://github.com/Bnocz/stonetop-steading

## Stack

- **API:** ASP.NET Core minimal API on .NET 10, EF Core + Npgsql
- **Frontend:** React + TypeScript + Vite, React Query
- **Database:** PostgreSQL (Neon free tier in production)
- **Tests:** xUnit + Testcontainers (API, real Postgres), Vitest (UI)
- **CI/CD:** GitHub Actions, build → test → deploy on push to `main`
- **Hosting:** Render free web service, one Docker image. The API serves the built React files from `wwwroot`.

## Layout

```
/src/Api            ASP.NET Core API (endpoints, EF Core, migrations)
/src/Api/Domain     Steading rules: stat ranges, labels, effective values
/src/web            React app
/tests/Api.Tests    xUnit + Testcontainers
Dockerfile          Multi-stage: build web → publish API → copy web into wwwroot
```

## Commands

```
dotnet test                       # API tests (needs Docker running for Testcontainers)
cd src/web && npm run dev         # frontend dev server
cd src/web && npm test            # frontend tests
docker build -t stonetop-steading .
```

## Configuration

- `ConnectionStrings__Default` — Postgres connection string
- `Admin__Username`, `Admin__Password` — the single GM account
- Never commit secrets. Local values go in user-secrets or an untracked `.env`.

## Hard rules

1. **GM-only data is filtered on the server.** Player endpoints never return rows where `gmOnly = true`. Never rely on the frontend to hide secrets.
2. **Every stat change writes a LogEntry in the same transaction.** The log can never disagree with the sheet.
3. **Never edit an existing test to make it pass.** If a test looks wrong, stop and explain why.
4. **Never invent game mechanics.** Use only the values in the mechanics reference below. If something is missing, ask.
5. **Plan before code.** For any feature, propose files to touch, tests to write and risks, then wait for approval.
6. **Write failing tests first**, then the implementation.
7. Store raw stat values; compute effective values (debility effects) in `/src/Api/Domain`.
8. Keep changes small: one feature per branch/PR.

## Conventions

- C#: nullable enabled, records for DTOs, async all the way, no business logic in endpoint lambdas.
- TypeScript: strict mode, no `any`, server state through React Query only.
- Endpoints: `/api/gm/...` (authenticated) and `/api/view/{shareToken}/...` (public, read-only).

---

## Steading mechanics reference

Source: Stonetop Book I, Steading playbook. Verified against the book.

### Size (descriptive, with modifier)

| Value | Label | Approx. people |
| --- | --- | --- |
| -1 | Hamlet | <50 |
| +0 | Village | 150–350 |
| +1 | Town | 500–1500 |
| +2 | City | 2500+ |

### Fortunes

- Range: -3 to +3. Resets seasonally via steading moves.
- Stonetop starts at **+1**.

### Surplus (resource)

- Integer, minimum 0. Spent by steading moves (e.g. *Pull Together*), gained through seasonal shifts.
- Stonetop starts at **1**.

### Prosperity

| -2 | -1 | +0 | +1 | +2 |
| --- | --- | --- | --- | --- |
| Dirt | Poor | Moderate | Wealthy | Rich |

Stonetop starts at **-1 (Poor)**.

### Defenses

| -2 | -1 | +0 | +1 | +2 | +3 |
| --- | --- | --- | --- | --- | --- |
| None | Militia | Watch | Guard | Garrison | Battalion |

Stonetop starts at **-1 (Militia)**.

### Population

| -2 | -1 | +0 | +1 | +2 |
| --- | --- | --- | --- | --- |
| Exodus | Shrinking | Steady | Growing | Booming |

Stonetop starts at **+0 (Steady)**.

### Debilities

| Debility | Cause | Effect |
| --- | --- | --- |
| Diminished | Injury or sickness | Disadvantage on *Deploy*, *Muster*, *Pull Together* |
| Lacking | Shortages, hoarding, distrust | Treat Prosperity as 1 lower |
| Malcontent | Fear, anger, despair | Forces Fortunes to reset lower and negatively impacts social cohesion moves |

The app only tracks and displays debilities and their effective-value changes. It does not roll moves.

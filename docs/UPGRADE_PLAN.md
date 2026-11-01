# Upgrade Plan (2026-11)

Status: **proposal, awaiting approval**. Nothing here has been pushed, deployed, or
reflected in the public README yet.

Scope rule: this project is secondary to embedded/AI work, so the plan targets a small
number of upgrades that produce *measurable, interview-defensible* evidence for a
backend/SWE resume. Every number that ends up in the README must come with how it was
measured (machine, dataset, tool, command, date). No estimated or invented numbers.

## Current state (verified 2026-11-01 on branch `main` @ `0834386`)

| Area | What the code actually does today |
| --- | --- |
| Tests | 6 xUnit integration tests (`WebApplicationFactory` + temp SQLite), all passing locally (`dotnet test`, .NET SDK 10.0.301). README still says "Total tests: 5". |
| Rate limiting | **Already present** in `Program.cs`: fixed window, 20 requests/min per remote IP, applied to the whole `AuthController` (`[EnableRateLimiting("auth")]`). That means `GET /api/auth/me` (called by the SPA on every page load, `App.vue:187`) and `logout` share the login budget. No test covers it. README still lists "Login rate limiting" under Future Improvements. |
| Health | One `/health` endpoint with a DB `CanConnectAsync` check. No liveness/readiness split. |
| Databases | SQLite (dev/tests, `EnsureCreated`) and SQL Server (prod, `Migrate`). **The SQL Server migrations are never executed in CI**: tests only use SQLite + `EnsureCreated`. |
| Deploy | `Dockerfile`, `render.yaml` (Render free + Azure SQL) and `DEPLOYMENT.md` exist; the GitHub repo has no homepage URL, so there is no evidence it is currently deployed. |
| Observability | `ILogger` structured logs only. No traces/metrics. |
| Performance | No measurements. Search uses `LIKE '%keyword%'` (cannot use an index) plus a separate `COUNT(*)` per page, which is a good candidate to measure. |
| Dev machine | Apple M4 Pro, 48 GB RAM, Docker 29.2.0 (arm64). `k6` not installed. |

## Ranked upgrade list

Effort is in focused working days. "Interview value" means how well the item turns into
a concrete, defensible story ("I measured X, found Y, changed Z").

| # | Upgrade | Effort | Interview value | Recommend |
| --- | --- | --- | --- | --- |
| 1 | Login-specific rate limiting + tests | 0.5 d | High (security, concrete policy design) | **Yes** |
| 2 | k6 load test with seeded dataset, measured RPS/p95, one targeted fix with before/after | 1–1.5 d | High (only item that produces real numbers) | **Yes** |
| 3 | Testcontainers SQL Server tests (migrations actually run) + unit tests + coverage report in CI | 1–1.5 d | High (closes a real gap: prod migrations untested) | **Yes** |
| 4 | Liveness/readiness split + Docker Compose dev stack (API + SQL Server) | 0.5 d | Medium (ops hygiene; needed by #2/#3 anyway) | **Yes** (small) |
| 5 | OpenTelemetry traces + metrics (OTLP exporter, Aspire dashboard in Compose) | 0.5–1 d | Medium | Optional / later |
| 6 | Free cloud deploy using existing `render.yaml` + Azure SQL free offer | 0.5 d + account setup | Medium (live URL), but free tier cold starts make it useless for perf numbers | Later, needs your accounts and approval |
| 7 | PostgreSQL as a provider | 1–1.5 d | Low here: prod already targets SQL Server; this would add a third provider and a second migration set | **No** |
| 8 | Email verification / password recovery / OIDC | 2–4 d | Low per day for this project | **No** |

Recommended set: **#1 + #2 + #3 + #4, about 3.5–4 days in total**. The order is
4 → 1 → 3 → 2, because the Compose/SQL Server setup from #4 is reused by #3 and #2.

---

### 1. Login-specific rate limiting + tests (0.5 d)

Now: one 20/min/IP fixed window shared by register, login, `/me`, and logout.

Change to:
- A separate `login` policy: sliding window, partitioned by client IP, applied only to
  `POST /api/auth/login` and `POST /api/auth/register`; `/me` and `logout` are no longer
  limited by it.
- A rejection that returns `429` as Problem Details with a `Retry-After` header.
- Limits read from configuration (`RateLimiting:Login:*`), so tests can use small values.
- Document how the client IP is determined behind Render's proxy
  (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`), and the known limitation that an in-memory
  limiter is per instance.
- Tests: the Nth+1 login inside the window returns 429 with `Retry-After`; `/me` is
  unaffected while login is throttled.

Files: `Program.cs`, `Controllers/AuthController.cs`, `appsettings.json`, new
`Configuration/RateLimitOptions.cs`, new `TaskManager.Api.Tests/RateLimitingTests.cs`,
`TaskManagerApiFactory.cs` (test config override).

### 2. k6 load test with measured numbers (1–1.5 d)

- `loadtest/seed`: deterministic seeder (fixed RNG seed) that creates a named dataset,
  proposed as **`ds-100x500`: 100 users × 500 tasks = 50,000 tasks**.
- `loadtest/k6/tasks.js`: logs in each virtual user once (cookie), then runs a mixed
  scenario (list/page, search, summary, create, status patch) with thresholds.
- Runs against the Release build in a container, on SQL Server (Compose) and on SQLite,
  on the M4 Pro. Records req/s, p50/p95/p99, and error rate.
- Find one bottleneck in the results (likely candidates: `LIKE '%kw%'` search, or
  `COUNT(*)` + page as two round trips), fix it, re-run with the same dataset and
  script, and report before/after.
- `docs/PERFORMANCE.md`: machine, OS, .NET/k6/DB versions, dataset, VUs/duration, exact
  commands, raw k6 summary JSON committed under `loadtest/results/`, date.

Files: new `loadtest/` (seeder project or script, k6 script, results), new
`docs/PERFORMANCE.md`, possibly `Services/TaskService.cs` and a migration for the fix.
Needs: `brew install k6` (local only).

### 3. Testcontainers + unit tests + coverage (1–1.5 d)

- `Testcontainers.MsSql` fixture: start SQL Server, run `Database.MigrateAsync()` (the real
  production path), then run the existing ownership and auth tests against it as well
  as against SQLite.
- Unit tests for `TaskService` logic (sorting allow-list, `ResolveStatus`, summary
  overdue/due-today boundaries, search trimming), plus `JwtTokenService` claims and expiry.
- `coverlet.collector` + ReportGenerator; CI uploads an HTML coverage report artifact and
  prints line coverage in the job summary. The coverage % reported is whatever CI measures.
- CI: SQL Server container tests run on `ubuntu-latest`. Locally on Apple Silicon the
  SQL Server image runs under emulation; the tests are tagged so they can be skipped
  if Docker is unavailable.

Files: `TaskManager.Api.Tests.csproj`, new `Fixtures/SqlServerFixture.cs`, new
`Unit/TaskServiceTests.cs`, `Unit/JwtTokenServiceTests.cs`, `TaskManagerApiFactory.cs`,
`.github/workflows/ci.yml`.

### 4. Liveness/readiness + Docker Compose (0.5 d)

- `/health/live` (process only, no dependencies) and `/health/ready` (DB check, JSON
  body); keep `/health` as an alias for `render.yaml` compatibility.
- `compose.yaml`: SQL Server + API (built from the existing `Dockerfile`), with a DB
  healthcheck and `depends_on: condition: service_healthy`. Optional profile for the
  Aspire dashboard if #5 is done later.
- Tests for both endpoints.

Files: `Program.cs`, `Infrastructure/DatabaseHealthCheck.cs` (tags), new `compose.yaml`,
`.env.example`, `HealthCheckTests.cs`, `DEPLOYMENT.md`.

### Not in the recommended set

- **5. OpenTelemetry**: worth adding later if the project returns to a backend resume;
  pairs well with #2 (traces to explain a slow p95). Files: `Program.cs`, csproj,
  `compose.yaml`.
- **6. Deploy**: the configuration already exists. It needs your Render and Azure accounts,
  and it is outward-facing, so it waits for an explicit go-ahead. Free tier is $0 but has
  cold starts; numbers from it would not be used.
- **7. PostgreSQL / 8. auth features**: low value per day for this repository.

## Approval list (numbered: now → change to)

Please approve by number (for example "1, 2, 3, 4") or edit.

1. **Rate limiting.** Now: one 20/min/IP window shared by all `/api/auth/*`, untested → change to: a login/register-only sliding-window policy, a 429 Problem Details response with `Retry-After`, configurable limits, and 2+ tests.
2. **Load test.** Now: no performance data → change to: a k6 script, a seeded `ds-100x500` dataset, measured req/s and p50/p95/p99 on an M4 Pro for SQL Server and SQLite, one bottleneck fix with before/after, and `docs/PERFORMANCE.md` with the method and raw results.
3. **Tests.** Now: 6 SQLite-only integration tests; prod migrations never run in CI → change to: a Testcontainers SQL Server suite that runs the real migrations, `TaskService`/`JwtTokenService` unit tests, and a coverage report in CI.
4. **Health + Compose.** Now: one `/health` → change to: `/health/live` and `/health/ready` (with `/health` kept), plus `compose.yaml` with SQL Server for local dev and load tests.
5. **Docs (local branch only).** Now: the README says 5 tests and lists rate limiting as future work → change to: the README updated to match items 1–4 *on the branch*; it reaches GitHub only after you approve a push.
6. **Not doing now:** OpenTelemetry, cloud deploy, PostgreSQL, email verification, OIDC (OTel and deploy can be revisited).

Work happens on the local branch `upgrade/2026-11-plan`; no push, PR, deploy, or public
README change without a separate approval.

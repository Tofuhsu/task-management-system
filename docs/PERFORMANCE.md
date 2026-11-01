# Performance and Load Testing

Every number on this page was measured on 2026-11-01 with the scripts in
[`loadtest/`](../loadtest). The raw k6 summaries are committed in
[`loadtest/results/`](../loadtest/results). These are numbers for one developer laptop,
not production capacity figures.

## Summary

Load testing showed that task search on SQL Server read the **whole `TaskItems`
table** (all users' rows) on every request. Adding `Title` and `Description` as included
columns on the existing `(UserId, CreatedAt)` index turned that into an index seek, with
these results on the same workload and dataset (mean of 3 runs each):

| Metric (all requests, 50 VUs, 60 s) | Before | After |
| --- | --- | --- |
| Throughput | 1,626 req/s | 3,216 req/s |
| p95 latency | 73.6 ms | 28.6 ms |
| p99 latency | 108.0 ms | 42.5 ms |
| Search p95 (`GET /api/tasks?search=`) | 107.5 ms | 26.9 ms |
| Logical reads for one search `COUNT(*)` | 1,320 | 14 |

## Environment

| Item | Value |
| --- | --- |
| Machine | Apple M4 Pro (14 cores: 10 performance + 4 efficiency), 48 GB RAM |
| OS | macOS 26.6.2 |
| Docker | Docker Desktop, engine 29.2.0; Linux VM with 14 CPUs and 7.65 GiB memory |
| API | Production image built from this repo's `Dockerfile` (ASP.NET Core runtime 10.0.14, `ASPNETCORE_ENVIRONMENT=Production`), one container |
| Database | SQL Server 2022 (`mcr.microsoft.com/mssql/server:2022-CU16-ubuntu-22.04`, Developer edition). The image is amd64-only and ran under **Rosetta emulation** |
| Load generator | k6 v2.3.0, running natively on the same machine (it competes with the API and database for CPU) |
| Network | `localhost` to Docker's published port, plain HTTP |

## Dataset: `ds-100x500`

[`loadtest/k6/seed.js`](../loadtest/k6/seed.js) creates the dataset through the public
API with a fixed-seed PRNG:

* 100 users (`loadtest-user-{0..99}@example.test`, test-only password in `lib.js`)
* 500 tasks per user, **50,000 tasks** in total
* Each title and description is made of random words from a 20-word vocabulary. 70% of
  tasks have a due date within 120 days of 2026-10-01; status and priority are uniform.

## Workload

[`loadtest/k6/tasks.js`](../loadtest/k6/tasks.js). `setup()` logs in all 100 users once.
Then 50 virtual users loop with **no think time** for 60 seconds (closed model). Each VU is
pinned to one user and picks an operation on each iteration:

| Share | Request |
| --- | --- |
| 40% | `GET /api/tasks?page={1..5}&pageSize=20&sortBy=CreatedAt&sortDirection=desc` |
| 20% | `GET /api/tasks?page=1&pageSize=20&search={random word}` |
| 15% | `GET /api/tasks/summary` |
| 15% | `POST /api/tasks` |
| 10% | `GET /api/tasks?page=1&pageSize=1`, then `PATCH /api/tasks/{id}/status` |

Because there is no think time, req/s is the throughput the stack sustains at 50
concurrent clients on this machine, and latency is measured at that saturation point.

## Procedure

```bash
cp .env.example .env              # generate secrets; set AUTH_RATE_LIMIT=1000
docker build -t tms:baseline .     # at the commit before the index change
docker build -t tms:covering-index .
loadtest/run.sh sqlserver-baseline tms:baseline
loadtest/run.sh sqlserver-covering-index tms:covering-index
```

[`run.sh`](../loadtest/run.sh) does the following for each of its 3 runs:
`docker compose down -v` (deletes the database), start SQL Server and the given API image,
wait for `/health/ready`, seed `ds-100x500`, then run the workload. So every run starts
from an identical 50,000-task database. The login rate limit is raised for the test
because all virtual users share one client IP.

## Results: SQL Server

Mean of 3 runs. The per-run range is given for the headline figures.

| | Baseline | Covering index |
| --- | --- | --- |
| Throughput (req/s) | 1,626 (1,607–1,659) | 3,216 (3,198–3,236) |
| All requests p95 (ms) | 73.6 (71.8–75.3) | 28.6 (28.2–28.8) |
| All requests p99 (ms) | 108.0 | 42.5 |
| Failed requests | 1 in each of runs 2 and 3 (one `search` check per run; status not captured) | 0 |

Per endpoint (ms, mean of 3 runs):

| Endpoint | Baseline med / p95 / p99 | Covering index med / p95 / p99 |
| --- | --- | --- |
| `GET /api/tasks` | 18.4 / 43.7 / 63.3 | 12.0 / 26.1 / 39.9 |
| `GET /api/tasks?search` | 57.0 / 107.5 / 144.7 | 13.1 / 26.9 / 41.0 |
| `GET /api/tasks/summary` | 8.6 / 19.9 / 29.6 | 7.3 / 16.7 / 27.0 |
| `POST /api/tasks` | 25.1 / 46.5 / 61.8 | 16.3 / 31.6 / 47.1 |
| `PATCH /api/tasks/{id}/status` | 31.3 / 56.7 / 73.7 | 19.6 / 37.2 / 54.4 |

## What the bottleneck was

Search was the slowest endpoint, so the SQL Server plan was captured for the `COUNT(*)` that
`TaskService.SearchAsync` issues for each page:

```sql
SELECT COUNT(*) FROM TaskItems t
WHERE t.UserId = @u
  AND (t.Title LIKE @p OR (t.Description IS NOT NULL AND t.Description LIKE @p));
-- @p = N'%review%'
```

* **Before:** `Clustered Index Scan` on `PK_TaskItems` with **1,320 logical reads**. The
  `(UserId, CreatedAt)` index finds one user's ~500 rows, but `Title` and `Description`
  are not in it. The optimizer judged ~500 key lookups to cost more than scanning every
  row in the table, so the cost of each search grew with the total number of tasks
  across *all* users.
* **After:** `IX_TaskItems_UserId_CreatedAt` with `INCLUDE (Title, Description)` (migration
  `CoverUserSearchIndex`). The plan becomes an `Index Seek` on `UserId` with **14 logical
  reads**.

Both read counts were measured with `SET STATISTICS IO ON` on a freshly seeded
`ds-100x500` database (one arbitrary user; 104 and 103 matches respectively, since user
IDs are assigned in a different order on each seed).

The other endpoints also got faster. They did not change, so the likely explanation is
that the search scans had been using up shared database CPU (made worse by Rosetta
emulation). This is an inference; it was not profiled separately.

Trade-off: the index now stores a copy of `Title` (up to 200 characters) and `Description`
(up to 2,000 characters) for every task, which costs storage and write I/O. Write latency
did not get worse in this workload, but the index size was not measured.

## Results: SQLite (one run, for reference)

The development provider, measured once with the same dataset and workload, before the
index change (the index change only affects SQL Server). The API ran in the same image
with `DatabaseProvider=Sqlite` and a database file inside the container:

| | SQLite |
| --- | --- |
| Throughput | 2,083 req/s |
| All requests p95 / p99 | 152.6 / 454.6 ms |
| Reads (`GET /api/tasks`, search, summary) p95 | 16.3–16.6 ms |
| Writes (`POST`, `PATCH`) p95 | 453.9–454.0 ms |

Reads are fast because the database is in-process, but writes queue behind SQLite's single
writer lock. This is expected and is the reason SQLite is only used for development.
Raw file: `loadtest/results/sqlite-baseline.json`.

## Caveats

* SQL Server ran under x86-64 emulation on an ARM host, so absolute numbers understate
  native performance. The before/after comparison uses identical conditions.
* k6, the API, and the database share one machine.
* One API instance. The in-memory rate limiter and every number here apply per instance.
* 3 runs per configuration. The run-to-run spread is shown above, and no other
  statistical testing was done.

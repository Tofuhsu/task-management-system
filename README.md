[![CI](https://github.com/Tofuhsu/task-management-system/actions/workflows/ci.yml/badge.svg)](https://github.com/Tofuhsu/task-management-system/actions/workflows/ci.yml)
# Task Management System

A full-stack, multi-user task dashboard built with ASP.NET Core, Entity Framework Core, Vue 3, and TypeScript. Users can register, sign in, and manage a private workspace with server-side search, filtering, sorting, pagination, and status summaries.

## Highlights

- JWT authentication with ASP.NET Core password hashing
- JWT stored in an `HttpOnly`, `SameSite=Strict` cookie instead of browser storage
- Per-user ownership checks on every task read and write
- RESTful CRUD endpoints with DTO and service layers
- Server-side pagination, keyword search, filters, and allow-listed sorting
- A single aggregate query for dashboard counts
- Data Annotation and cross-field request validation
- RFC-style Problem Details with a request `traceId`
- Centralized exception handling, structured logs, and cancellation tokens
- SQLite for zero-setup local development on macOS and a SQL Server provider/migration path
- Typed Vue 3 interface with authentication, loading, empty, validation, and API error states
- API integration tests for authentication, secure cookies, and cross-account task isolation
- GitHub Actions CI for backend tests and frontend audit, type checking, and production build

## Tech stack

| Layer | Technology |
| --- | --- |
| API | ASP.NET Core 10 Web API, JWT Bearer authentication, Swagger / OpenAPI |
| Data | Entity Framework Core 10, SQLite (local), SQL Server (supported provider) |
| Frontend | Vue 3, TypeScript, Axios, Vite |
| Testing | xUnit, ASP.NET Core `WebApplicationFactory`, isolated SQLite databases |

## Architecture

```text
Vue UI -> Auth/Tasks Controllers -> Services -> AppDbContext -> SQLite or SQL Server
             |                       |
             +-- HttpOnly JWT -------+-- UserId ownership filter
```

The controllers own HTTP and authentication concerns, services own task and account behavior, DTOs define the public API contract, and EF Core handles persistence. Task IDs are always queried together with the authenticated `UserId`, so another user's task returns `404` even if its ID is known.

## Run locally on macOS

### Prerequisites

- .NET 10 SDK
- Node.js 20.19+ or 22.12+
- npm

Development uses SQLite, so SQL Server and Docker are not required.

### 1. Prepare the new authentication schema

Stop the API first. If an older `taskmanager.db` exists, preserve it under another name because `EnsureCreated` cannot modify an existing SQLite schema:

```bash
cd backend/TaskManager.Api
[ -f taskmanager.db ] && mv taskmanager.db taskmanager-pre-auth.db
[ -f taskmanager.db-wal ] && mv taskmanager.db-wal taskmanager-pre-auth.db-wal
[ -f taskmanager.db-shm ] && mv taskmanager.db-shm taskmanager-pre-auth.db-shm
```

The renamed file is only a backup of pre-authentication demo data. The application will create a new database automatically.

### 2. Create a local JWT signing key

The repository intentionally contains no signing secret. Store a random local key with .NET user secrets:

```bash
JWT_KEY=$(openssl rand -base64 48)
dotnet user-secrets set "Jwt:SigningKey" "$JWT_KEY"
unset JWT_KEY
```

Do not commit production keys to source control.

### 3. Start the API

```bash
dotnet restore
dotnet build
dotnet run
```

The API defaults to `http://localhost:5100`. Swagger is available at `http://localhost:5100/swagger` in Development.

### 4. Start the frontend in a second terminal

```bash
cd frontend/task-manager-web
cp .env.example .env.local
npm ci
npm run build
npm run dev
```

Open `http://localhost:5173`, register an account, and create a task.

### 5. Verify user isolation

1. Register account A and create a task.
2. Sign out.
3. Register account B with a different email.
4. Confirm that account B starts with zero tasks.
5. Sign back in as account A and confirm its task is still present.

This proves both the user experience and the server-side ownership filter. The client hiding another user's task is not the security boundary; the API query is.

## Automated tests

The integration suite starts the real ASP.NET Core application in memory and creates a unique temporary SQLite database. It does not require a running API, a local JWT secret, or access to `taskmanager.db`.

```bash
cd backend
dotnet test TaskManager.sln
```

The suite verifies unauthenticated access, secure authentication cookies, rejected credentials, case-insensitive duplicate emails, and read/write isolation between two accounts.

## Continuous integration

`.github/workflows/ci.yml` runs on every push and pull request to `main`. The backend job restores, builds, and runs the integration tests. The frontend job performs a clean install, checks dependency vulnerabilities, runs the TypeScript checker, and creates a production build.

## API

Authentication endpoints:

| Method | Endpoint | Description |
| --- | --- | --- |
| `POST` | `/api/auth/register` | Create an account and authentication cookie |
| `POST` | `/api/auth/login` | Verify credentials and refresh the cookie |
| `GET` | `/api/auth/me` | Return the authenticated user |
| `POST` | `/api/auth/logout` | Expire the authentication cookie |

Protected task endpoints:

| Method | Endpoint | Description |
| --- | --- | --- |
| `GET` | `/api/tasks` | Search, filter, sort, and paginate the user's tasks |
| `GET` | `/api/tasks/{id}` | Get one owned task |
| `GET` | `/api/tasks/summary` | Get the user's dashboard counts |
| `POST` | `/api/tasks` | Create an owned task |
| `PUT` | `/api/tasks/{id}` | Replace editable fields on an owned task |
| `PATCH` | `/api/tasks/{id}/status` | Change the status of an owned task |
| `DELETE` | `/api/tasks/{id}` | Delete an owned task |

Example query:

```http
GET /api/tasks?page=1&pageSize=10&search=backend&status=Todo&priority=High&sortBy=DueDate&sortDirection=asc
```

Supported sort fields are `CreatedAt`, `Title`, `DueDate`, `Priority`, and `Status`. Page size is limited to 100.

## Security decisions

- Passwords are stored only as salted hashes through `PasswordHasher<TUser>`; plaintext passwords are never persisted or logged.
- JWT signature, issuer, audience, lifetime, and expiration are validated on every protected request.
- The browser receives the JWT in an `HttpOnly` cookie, so frontend JavaScript cannot read it.
- `SameSite=Strict` and an exact CORS allow-list reduce cross-site request risk for the current same-site deployment model.
- The API also accepts a standard `Authorization: Bearer <token>` header for non-browser clients.
- Login errors do not reveal whether the email or password was incorrect.

This built-in account flow is appropriate for a closed portfolio application. A public production system should normally use a managed OpenID Connect/OAuth provider, add account verification/recovery, login rate limiting, key rotation, and deployment-specific CSRF review.

## SQL Server

Set `DatabaseProvider` to `SqlServer`, override `ConnectionStrings__DefaultConnection`, and run:

```bash
dotnet ef database update
```

The authentication migration is safe for a fresh database. It deliberately stops if an older SQL Server database already contains unowned tasks; those rows require an explicit ownership migration instead of silently assigning them to the wrong user.

## Error contract

Invalid requests return `400 application/problem+json` with field-level messages. Expected authentication conflicts return `401` or `409`. Unexpected failures return a safe `500` response and log the underlying exception on the server.

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "PageSize": ["The field PageSize must be between 1 and 100."]
  },
  "traceId": "00-..."
}
```

## Roadmap

1. Login rate limiting and a production identity provider option
2. Docker Compose for the API, UI, and SQL Server
3. Cloud deployment and documented performance measurements

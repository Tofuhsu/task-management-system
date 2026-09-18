# Deployment

The production image serves the compiled Vue application and the ASP.NET Core API from one origin. This keeps the authentication cookie same-site and avoids a separate frontend deployment.

## Architecture

```text
Browser -> Render Web Service (Vue + ASP.NET Core API) -> Azure SQL Database
                     |
                     +-> GET /health checks database connectivity
```

The application applies pending SQL Server migrations during startup. Use a fresh database for the first deployment.

## Required production configuration

| Environment variable | Purpose |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT=Production` | Enables production middleware and secure cookies |
| `DatabaseProvider=SqlServer` | Selects the SQL Server EF Core provider |
| `ConnectionStrings__DefaultConnection` | Azure SQL connection string; keep secret |
| `Jwt__Issuer` | JWT issuer |
| `Jwt__Audience` | JWT audience |
| `Jwt__SigningKey` | Random signing key with at least 32 characters; keep secret |
| `Jwt__ExpirationMinutes` | Token lifetime between 5 and 1440 minutes |

Never commit production connection strings or signing keys.

## Recommended portfolio deployment

Use the repository's `render.yaml` to create one free Render web service. Use a persistent Azure SQL Database instead of SQLite because Render's free filesystem is ephemeral and a local SQLite database would be deleted after restarts or idle spin-downs.

### 1. Create Azure SQL Database

1. Sign in to the Azure portal and open **Azure SQL**.
2. Select **Create SQL Database** and choose the free database offer when available.
3. Create a new logical SQL server and a fresh database named `TaskManagerDb`.
4. Choose the option that pauses the database when the monthly free allowance is exhausted.
5. Allow the outbound IP ranges shown by the Render service in the Azure SQL server firewall.
6. Copy the ADO.NET connection string and replace the username and password placeholders locally before entering it in Render.

The value must resemble:

```text
Server=tcp:<server>.database.windows.net,1433;Initial Catalog=TaskManagerDb;Persist Security Info=False;User ID=<user>;Password=<password>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

Do not paste the real value into source files, issues, screenshots, or chat messages.

### 2. Create the Render Blueprint

1. Push the deployment files to the GitHub `main` branch and wait for CI to pass.
2. In Render, choose **New > Blueprint** and select this repository.
3. Render reads `render.yaml` and prompts for `ConnectionStrings__DefaultConnection`.
4. Paste the Azure SQL connection string into that secret field.
5. Create the Blueprint. Render generates `Jwt__SigningKey` automatically.
6. In the service dashboard, copy every outbound IP range into the Azure SQL server firewall if the first health check cannot reach the database.

The first start can take longer because the application applies migrations and Azure SQL may need to resume.

### 3. Smoke test

After Render reports a successful health check:

1. Open the service URL and register account A.
2. Create, edit, complete, filter, and delete a task.
3. Sign out, register account B, and confirm account A's tasks are not visible.
4. Sign back in as account A and confirm its remaining data is present.
5. Open `/health` and confirm it returns HTTP 200.
6. Redeploy the same commit and verify account data remains present.

## Local production-container test

Build the image:

```bash
docker build -t task-management-system .
```

Run it with an existing SQL Server connection string:

```bash
docker run --rm -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e DatabaseProvider=SqlServer \
  -e 'ConnectionStrings__DefaultConnection=<connection-string>' \
  -e Jwt__Issuer=TaskManager.Api \
  -e Jwt__Audience=TaskManager.Web \
  -e 'Jwt__SigningKey=<random-key-at-least-32-characters>' \
  task-management-system
```

Open `http://localhost:8080` only when HTTPS redirection is disabled or TLS is terminated by a local proxy. The intended production environment terminates TLS at the hosting platform.

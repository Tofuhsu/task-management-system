# Task Management System

A full-stack task management system built with ASP.NET Core Web API, Entity Framework Core, SQL Server, and Vue 3.

## Tech Stack

### Backend
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- Swagger

### Frontend
- Vue 3
- Axios

---

# Features

## Backend
- RESTful API
- CRUD operations
- DTO architecture
- Service layer
- Pagination
- Filtering
- Sorting
- Summary API
- Entity Framework Core migrations

## Database
- SQL Server integration
- Entity relationships
- Indexes
- Query optimization

## Frontend
- Task list
- Create task
- Update task
- Delete task

---

# API Endpoints

## Tasks

### Get tasks
GET /api/tasks

### Get task by id
GET /api/tasks/{id}

### Create task
POST /api/tasks

### Update task
PUT /api/tasks/{id}

### Update task status
PATCH /api/tasks/{id}/status

### Delete task
DELETE /api/tasks/{id}

### Get summary
GET /api/tasks/summary

---

# Query Features

Examples:

```http
GET /api/tasks?page=1&pageSize=10
GET /api/tasks?search=backend
GET /api/tasks?status=Todo
GET /api/tasks?priority=High
GET /api/tasks?sortBy=DueDate&sortDirection=asc
```

---

# Architecture

```text
Vue Frontend
    ↓
ASP.NET Core Web API
    ↓
Service Layer
    ↓
Entity Framework Core
    ↓
SQL Server
```

---

# Database

- SQL Server Express
- EF Core Migrations
- Indexed columns for filtering and sorting

---

# Future Improvements

- JWT Authentication
- User-based tasks
- Role-based authorization
- Docker support
- Cloud deployment
- Logging & middleware
- FluentValidation
# InvoiceBilling Architecture

## 1. Purpose
InvoiceBilling is an enterprise-style application used to demonstrate:
- Clean separation of concerns (Domain / Infrastructure / API / UI)
- RESTful API design with DTOs
- Data persistence using EF Core migrations
- Incremental feature development (Customers, Products, Invoices next)
- CI pipeline running backend + frontend builds

This repo is intentionally designed to be clone-and-run for reviewers/interviewers.

---

## 2. High-Level Architecture
The solution is a modular monolith:
- One backend API (ASP.NET Core .NET 8)
- One frontend UI (React + TypeScript + Vite)
- One database for local development (SQLite)

### Deployment Topology (local/dev)
- React dev server runs on: `http://localhost:5173`
- API runs on: `http://localhost:5027`
- SQLite DB file stored locally (configured by API connection string)

---

## 3. Solution Structure
Repository layout:

- `src/InvoiceBilling.Api`
  - ASP.NET Core API (controllers, DTOs, startup)
  - Defines HTTP contracts and request/response shapes
  - Hosts Swagger/OpenAPI and Health endpoint
- `src/InvoiceBilling.Domain`
  - Pure domain entities (no EF Core / no HTTP / no infrastructure concerns)
  - Examples: `Customer`, `Product`
- `src/InvoiceBilling.Infrastructure`
  - EF Core DbContext, migrations, entity configurations
  - Dependency injection registrations (Infrastructure layer services)
- `frontend/invoice-ui`
  - React + TypeScript UI
  - Calls backend via `fetch` wrapper and API modules
- `docs`
  - Architecture and deployment documentation
- `.github/workflows`
  - CI pipeline definition (GitHub Actions)

---

## 4. Layering and Dependencies
Dependency direction is intentionally one-way:

- `Api` references `Infrastructure` and `Domain`
- `Infrastructure` references `Domain`
- `Domain` references nothing

This keeps the Domain clean and testable and prevents UI/API concerns from leaking into core business entities.

---

## 5. Modules Implemented (so far)

### 5.1 Customers Module
Backend:
- Endpoints:
  - `GET /api/customers`
  - `GET /api/customers/{id}`
  - `POST /api/customers`
  - `PUT /api/customers/{id}`
  - `DELETE /api/customers/{id}` (soft delete)
- Uses DTOs:
  - `CustomerDto`
  - `CreateCustomerRequest`
  - `UpdateCustomerRequest`

Frontend:
- Customers page supports:
  - list, create, edit, delete
- Uses an API module and shared HTTP helper

### 5.2 Products Module
Backend:
- Endpoints:
  - `GET /api/products`
  - `GET /api/products/{id}`
  - `POST /api/products`
  - `PUT /api/products/{id}`
  - `DELETE /api/products/{id}` (soft delete)
- Uses DTOs:
  - `ProductDto`
  - `CreateProductRequest`
  - `UpdateProductRequest`

Frontend:
- Products page supports:
  - list, create, edit, delete
- Uses:
  - `src/api/productsApi.ts`
  - `src/features/products/*`

---

## 6. Data Model and Persistence

### 6.1 EF Core
- EF Core is used for ORM and schema migrations.
- Entity configurations are stored in separate classes in Infrastructure.
- DbContext applies configurations automatically using:
  - `modelBuilder.ApplyConfigurationsFromAssembly(...)`

### 6.2 Local Database Choice (SQLite)
For local clone-and-run:
- SQLite is used so no external DB server is required.
- DB file location is controlled by the connection string.

### 6.3 Soft Delete
Both Customer and Product use:
- `IsActive` boolean flag
- DELETE endpoints mark `IsActive = false`
- List endpoints filter `IsActive = true`

This mimics common enterprise behavior (auditability + safer deletes).

---

## 7. API Design Conventions
- Controllers return DTOs (not EF entities)
- Consistent route patterns:
  - `GET /api/{resource}`
  - `GET /api/{resource}/{id}`
  - `POST /api/{resource}`
  - `PUT /api/{resource}/{id}`
  - `DELETE /api/{resource}/{id}`
- Validation:
  - Minimal validation currently done in controllers (e.g., required Name)
  - Will evolve to dedicated validation (FluentValidation or pipeline behavior) later

---

## 8. Cross-Cutting Concerns

### 8.1 CORS
CORS is enabled for frontend-to-backend local development.
Origins are configuration-driven:
- `Cors:AllowedOrigins` in appsettings

### 8.2 Health Check
Health endpoint:
- `GET /health`
Used by CI and deployment environments (container orchestrators, load balancers, probes).

---

## 9. Frontend Architecture
- React + TypeScript + Vite
- Simple feature folder structure:
  - `src/features/customers`
  - `src/features/products`
- API access pattern:
  - `src/api/http.ts` provides a typed wrapper
  - `src/api/*Api.ts` encapsulates endpoint calls
This isolates network logic from UI components and scales better than inline fetch calls.

---

## 10. CI (Continuous Integration)
GitHub Actions workflow:
- Restores, builds, and tests backend
- Installs and builds frontend
- Ensures PRs/changes do not break build

Future upgrades:
- artifact publishing (`dotnet publish`, UI `dist/`)
- test coverage report
- Docker build/push (optional)

---

## 11. Roadmap (Next Modules)
Planned increments:
- Invoices (Invoice + InvoiceLine) with totals/taxes
- PDF generation & storage (LocalStack S3 / Azure Blob emulator)
- Background jobs (SQS / Azure queues + worker)
- Authentication/Authorization (JWT)
- Observability (structured logging, tracing)

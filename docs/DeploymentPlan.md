
---

## `docs/DeploymentPlan.md`

```markdown
# InvoiceBilling – Deployment Plan

## 1. Objectives

The deployment plan for InvoiceBilling aims to:

- Keep local development simple and fast.
- Prepare a clear path to deploy the application to the cloud.

The target cloud platform assumed in this plan is **Microsoft Azure**, but the structure is generic enough to adapt to other providers.

---

## 2. Environments

Planned environments:

1. **Local Development**
   - Backend: .NET 8 Web API running via `dotnet run`.
   - Frontend: Vite dev server (`npm run dev`).
   - Database: SQLite (`invoicebilling.db` file).

2. **Cloud (Future)**
   - Backend: hosted on Azure (App Service or Container Apps).
   - Frontend: hosted as static content (Azure Static Web Apps or App Service).
   - Database: managed relational database (e.g., Azure SQL or Azure Database for PostgreSQL).
   - CI/CD: GitHub Actions for build, test, and deploy.

---

## 3. Local Development Setup

### 3.1 Backend (API)

- Run the API from the solution root:

  ```bash
  dotnet ef database update -p src/InvoiceBilling.Infrastructure -s src/InvoiceBilling.Api
  dotnet run --project src/InvoiceBilling.Api

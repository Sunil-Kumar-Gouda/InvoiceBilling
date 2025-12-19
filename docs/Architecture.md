# InvoiceBilling – Architecture Overview

## 1. Goals

InvoiceBilling is a small but realistic invoice and billing system designed to:

- Demonstrate clean, layered architecture using .NET 8.
- Showcase a full-stack implementation with a React + TypeScript SPA.
- Be easy to run locally (SQLite, Vite dev server).
- Be ready to evolve towards cloud deployment (e.g., Azure).

The architecture follows a simplified Clean Architecture / Onion Architecture style with clear separation of concerns.

---

## 2. Solution Structure

The backend solution is organized into the following projects:

- `InvoiceBilling.Domain`
- `InvoiceBilling.Application`
- `InvoiceBilling.Infrastructure`
- `InvoiceBilling.Api`

The frontend lives in:

- `frontend/invoice-ui` (Vite + React + TypeScript)

### 2.1 Project Responsibilities

#### `InvoiceBilling.Domain`
- Contains the **core business model**.
- Holds entities and value objects (e.g., `Customer`, later `Invoice`, `InvoiceLine`, `Product`).
- Contains **no infrastructure or framework dependencies** (no EF Core, no ASP.NET types).
- Represents the “heart” of the system.

#### `InvoiceBilling.Application`
- Intended for **application-level logic** and use cases.
- Will contain:
  - Commands, queries, and handlers (CQRS style), e.g., `CreateInvoiceCommand`, `GetInvoicesQuery`.
  - Interfaces for repositories and services (e.g., `ICustomerRepository`).
  - Validation logic (e.g., FluentValidation) and cross-cutting patterns.
- Depends only on `Domain`, not on Infrastructure or Api.

> Note: On Day 1 this project may be mostly empty or minimal; it is intentionally reserved for use case orchestration as the project grows.

#### `InvoiceBilling.Infrastructure`
- Contains **technical implementations** that support the application:
  - Entity Framework Core DbContext (`InvoiceBillingDbContext`).
  - EF Core entity configurations (e.g., `CustomerConfiguration`).
  - Data access implementations (future repositories).
- Responsible for persistence and integration with external resources.
- Depends on `Domain` and EF Core packages.
- Exposes an extension method `AddInfrastructure(...)` to register its services into the DI container.

#### `InvoiceBilling.Api`
- ASP.NET Core Web API (.NET 8).
- Acts as the **entry point / host** for the backend:
  - Contains `Program.cs`, DI setup, HTTP pipeline configuration.
  - Exposes REST API endpoints (e.g., `/api/customers`, later `/api/invoices`, `/api/products`).
  - Configures CORS for the React SPA.
  - Uses `AddInfrastructure(...)` to plug in the Infrastructure layer.
- Depends on `Application` and `Infrastructure`.

---

## 3. Backend Architecture

### 3.1 Entity Framework Core & DbContext

- The application uses **Entity Framework Core** with **SQLite** for local development.
- `InvoiceBillingDbContext` lives in the `Infrastructure` project and exposes `DbSet<T>` properties, e.g.:

  ```csharp
  public DbSet<Customer> Customers => Set<Customer>();

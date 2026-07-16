# AP Flow — Solution Structure

**Status:** Approved — Permanent Reference
**Audience:** All AI development agents and human engineers
**Purpose:** This document defines the permanent solution structure for AP Flow. It must be read before placing any new code in the solution.

---

## 1. Solution Overview

| Project | Purpose |
|---|---|
| **APFlow.Api** | Entry point for all client-facing HTTP requests. Exposes controllers/endpoints, handles authentication, request/response mapping. Contains no business logic. |
| **APFlow.Application** | Application/use-case layer. Orchestrates business workflows, defines interfaces for infrastructure and integrations, contains application-level validation and DTOs. |
| **APFlow.Domain** | Core business domain. Entities, value objects, domain rules, and enums. The most stable and dependency-free project in the solution. |
| **APFlow.Infrastructure** | Implementations of Application interfaces for persistence and platform concerns — Azure SQL, Blob Storage, Key Vault, Service Bus. |
| **APFlow.Integrations** | Implementations of Application interfaces for external systems — Microsoft Graph, accounting system connectors (Sage 50, and future integrations), Azure AI Document Intelligence, Azure OpenAI. |
| **APFlow.Workers** | Background and asynchronous processing — polling, message-triggered processing, scheduled jobs. |
| **APFlow.Web** | Frontend SPA (React + TypeScript). Consumes APFlow.Api only; contains no business logic. |
| **Tests** | All automated tests, mirroring the structure of the projects under test. |
| **Docs** | Permanent and evolving project documentation, including this AI context set. |
| **Infrastructure** | Infrastructure-as-code and Azure resource definitions. Not to be confused with APFlow.Infrastructure. |
| **Scripts** | Build, deployment, and developer utility scripts. |

---

## 2. Dependency Rules

- **APFlow.Domain** has no dependencies on any other project.
- **APFlow.Application** → APFlow.Domain
- **APFlow.Api** → APFlow.Application
- **APFlow.Infrastructure** → APFlow.Application
- **APFlow.Integrations** → APFlow.Application
- **APFlow.Workers** → APFlow.Application
- **APFlow.Web** has no direct project reference; it communicates with the solution only via APFlow.Api over HTTP.
- No project may reference APFlow.Api except APFlow.Web (via HTTP, not project reference).
- No project may bypass APFlow.Application to reference APFlow.Domain directly, except where already permitted above.
- Infrastructure and Integrations must never reference each other.

---

## 3. Folder Structure

Each project follows a consistent internal layout appropriate to its role:

- **APFlow.Api** — Controllers, Middleware, Extensions, Configuration.
- **APFlow.Application** — Features (grouped by business capability), Interfaces, DTOs, Validation, Common.
- **APFlow.Domain** — Entities, ValueObjects, Enums, Exceptions.
- **APFlow.Infrastructure** — Persistence (data access), Storage, Messaging, Security.
- **APFlow.Integrations** — grouped by external system (e.g. Graph, Sage, DocumentIntelligence, OpenAI).
- **APFlow.Workers** — grouped by job/process type.
- **APFlow.Web** — pages/routes, components, api (client calls), auth.
- **Tests** — mirrors the folder structure of the project under test, one test project per source project.

---

## 4. Naming Conventions

- **Projects:** `APFlow.<Layer>` (e.g. `APFlow.Application`).
- **Namespaces:** match project and folder structure exactly (e.g. `APFlow.Application.Features.Invoices`).
- **Classes:** PascalCase, descriptive nouns (e.g. `InvoiceIngestionService`).
- **Interfaces:** prefixed with `I` (e.g. `IInvoiceRepository`).
- **Services:** suffixed with `Service` (e.g. `BlobStorageService`).
- **Repositories:** suffixed with `Repository` (e.g. `InvoiceRepository`).
- **Controllers:** suffixed with `Controller` (e.g. `InvoicesController`).
- **DTOs:** suffixed with `Dto` or `Request`/`Response` as appropriate (e.g. `InvoiceDto`).
- **Entities:** plain domain nouns, no suffix (e.g. `Invoice`).
- **Enums:** singular descriptive nouns (e.g. `InvoiceStatus`).
- **Configuration:** suffixed with `Options` or `Settings` (e.g. `BlobStorageOptions`).

---

## 5. Ownership

- **Business logic** is owned by APFlow.Domain and APFlow.Application.
- **Persistence** is owned by APFlow.Infrastructure.
- **External system integrations** are owned by APFlow.Integrations.
- **User interface** is owned by APFlow.Web.
- **Background/asynchronous processing** is owned by APFlow.Workers.
- **APFlow.Api** owns request handling and orchestration entry points only — it does not own business logic.

---

## 6. AI Agent Rules

- Agents must place code only in the project that owns the relevant responsibility, as defined in Section 5.
- Agents must not duplicate responsibilities across projects.
- Agents must not create new top-level projects or reorganise the solution structure without approval.
- Agents must follow the dependency rules in Section 2 without exception.

# AP Flow — Project Standards

**Status:** Approved — Permanent Reference
**Audience:** All AI development agents and human engineers
**Purpose:** This document defines the permanent engineering standards for AP Flow. It must be read before producing any code.

---

## 1. Engineering Principles

- Simplicity first — favour the simplest solution that correctly meets the requirement.
- Production-quality code — no throwaway or "temporary" code paths.
- Readability over cleverness — code is read far more often than it is written.
- Clean Architecture — clear separation of concerns between domain, application, and infrastructure layers.
- SOLID principles applied consistently.
- DRY where appropriate — avoid duplication, but not at the cost of clarity.
- Small, focused classes and methods — each with a single, clear responsibility.

---

## 2. General Rules

- Do not invent business requirements.
- Do not change approved architecture.
- Do not rename projects, namespaces, or folders.
- Do not introduce new frameworks or libraries without approval.
- Prefer built-in .NET and Azure capabilities over third-party alternatives.
- Keep dependencies to a minimum.

---

## 3. Coding Standards

- Use meaningful, descriptive names for classes, methods, and variables.
- Write self-explanatory code that does not depend on comments to be understood.
- Avoid unnecessary comments; comment only where intent is not obvious from the code itself.
- Validate all inputs, including internal method inputs where failure would be costly.
- Handle exceptions appropriately; do not swallow errors or fail silently.
- Use asynchronous programming where appropriate, particularly for I/O-bound operations.

---

## 4. Security Standards

- Never hardcode secrets, connection strings, or keys.
- Use Azure Key Vault for all secrets and sensitive configuration.
- Follow least privilege for all identities, roles, and permissions.
- Validate all external inputs, including data from users, APIs, and third-party systems.
- Protect tenant isolation at every layer where tenant data is accessed or stored.

---

## 5. Testing Standards

- Every feature requires unit tests.
- Business logic must be testable in isolation from infrastructure concerns.
- No production code is considered complete without accompanying tests.

---

## 6. Documentation Standards

- Public APIs require documentation describing purpose, inputs, and outputs.
- Complex logic requires a concise explanation of intent, not a line-by-line narration.
- Keep README files up to date as the system evolves.

---

## 7. AI Agent Rules

- If requirements are unclear, ask — do not proceed on assumption.
- Never guess at business logic, architecture, or intent.
- Never fabricate implementation details not provided in the work package or approved documentation.

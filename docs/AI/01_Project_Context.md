# AP Flow — Project Context

**Status:** Approved — Permanent Reference
**Audience:** All AI development agents and human engineers
**Purpose:** This document provides stable project context that must be read before performing any development task. It does not change during normal sprint execution.

---

## 1. Project Overview

AP Flow is a cloud-native, multi-tenant SaaS platform for Accounts Payable (AP) automation. It is designed to serve both small-to-medium enterprises (SMEs) and larger enterprise customers, automating the capture, processing, and approval of supplier invoices while integrating with existing accounting systems.

---

## 2. Product Vision

AP Flow exists to remove manual, repetitive work from the accounts payable process while keeping humans firmly in control of financial decisions. It automates invoice capture and data extraction, presents structured information for review, and integrates with the customer's accounting system of record — enabling finance teams to process invoices faster, with a clear audit trail, and without sacrificing control or compliance.

---

## 3. Core Business Objectives

- Reduce manual AP processing effort and data entry.
- Maintain human approval at all points where financial commitments are made.
- Improve auditability of the invoice lifecycle, end to end.
- Support multiple accounting systems, not a single vendor.
- Be AI-ready: architected so AI capabilities can be introduced and expanded over time.
- Be scalable and secure, suitable for both SME and enterprise customers.

---

## 4. Target Customers

- **Initial customer:** GB Skips.
- **Version 1 accounting integration:** Sage 50.
- **Future accounting integrations:** Xero, QuickBooks, Dynamics 365 Business Central, SAP Business One.

---

## 5. Approved Technology Stack

- .NET 9
- ASP.NET Core
- React
- TypeScript
- Azure SQL
- Azure Blob Storage
- Azure Service Bus
- Azure Key Vault
- Azure AI Document Intelligence
- Azure OpenAI
- Microsoft Graph
- Microsoft Entra External ID
- Azure App Service
- GitHub
- GitHub Actions

This is the complete approved stack. No other platforms, frameworks, or vendors are to be introduced without architectural approval.

---

## 6. Architecture Principles

- Multi-tenant by design, with clear tenant data isolation.
- Cloud-native, built on Azure managed services rather than self-hosted infrastructure.
- API-first backend, consumed by a decoupled frontend SPA.
- Human-in-the-loop by default; automation assists but does not replace approval steps.
- Event/message-driven where workflows span asynchronous steps (e.g. document ingestion, processing).
- Security and identity centralised through Microsoft Entra External ID.
- Secrets and configuration centralised through Azure Key Vault; nothing sensitive in source control.
- Auditability built into the data model, not bolted on afterward.
- Extensible integration layer, allowing new accounting systems to be added without core rework.
- AI capabilities (document extraction, future reasoning) treated as pluggable services, not hard dependencies of core workflow.

---

## 7. Project Goals

- Deliver a working MVP quickly without compromising core architecture.
- Prove the core invoice ingestion-to-review pipeline end to end.
- Establish a foundation that scales to multiple tenants and accounting systems.
- Keep the platform secure, auditable, and maintainable from day one.

---

## 8. Out of Scope for MVP

- Weighsoft integration.
- Automatic payment execution.
- Banking integrations.
- Mobile application.
- Complex AI decision-making.

---

## 9. AI Agent Instruction

AI agents must not invent requirements, change the architecture, rename projects, or introduce technologies not listed in the Approved Technology Stack, without explicit approval from the Chief Technical Architect. Any ambiguity or perceived gap in requirements must be raised as a question, not resolved by assumption.

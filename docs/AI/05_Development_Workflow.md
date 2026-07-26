# AP Flow — Development Workflow

**Status:** Approved — Permanent Reference
**Audience:** All AI development agents and human engineers
**Purpose:** This document defines how AI agents collaborate during development. It must be read before beginning any work package.

---

## 1. Product Owner

Defines business requirements.

## 2. Chief Technical Architect

Breaks requirements into Developer Work Packages.

## 3. Backend Engineer

Implements backend work packages.

## 4. Frontend Engineer

Implements frontend work packages.

## 5. DevOps Engineer

Implements infrastructure and deployment work packages.

## 6. QA Engineer

Reviews implementation and prepares test cases.

---

## 7. Review Process

1. Chief Technical Architect reviews completed work.
2. QA reviews completed work.
3. Developer fixes issues.
4. Code is committed to Git.

---

## 8. Git Workflow

- One work package per commit.
- Meaningful commit messages.
- Small incremental changes.
- Build before commit.
- Test before commit.
- Every work package that changes the EF Core model must include a generated migration (`dotnet ef migrations add`) as a deliverable — never a hand-written SQL file. (Added following WP-046/WP-052's discovery that no migration mechanism existed at all; see WP-052 Part A.)

---

## 8a. Source of Truth for `docs/AI/*.md`

`01_Project_Context.md` through `06_Domain_Reference_Data.md` must be committed to the repository as tracked files — not supplied only as chat context, and not shadowed by parallel addendum files. If a document needs updating, edit it directly and commit the change; do not create a `*_Addendum.md` alongside it. A stale, uncommitted copy of `06_Domain_Reference_Data.md` was the direct cause of an incorrect assumption in WP-053 — this section exists so that doesn't recur.

---

## 9. AI Agent Rules

- Implement only the assigned work package.
- Never implement future work.
- Do not change unrelated files.
- Escalate unclear requirements to the Chief Technical Architect.

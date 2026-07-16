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

---

## 9. AI Agent Rules

- Implement only the assigned work package.
- Never implement future work.
- Do not change unrelated files.
- Escalate unclear requirements to the Chief Technical Architect.

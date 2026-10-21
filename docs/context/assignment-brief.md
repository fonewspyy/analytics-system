# Assignment brief — Containerized Business Analytics System

> Source: `Containerized Business Analytics System_C_.pdf` (received 2026-10-21).
> §3 Day 3, §4 and §5 are quoted in the original Thai (from a copy of the brief), each with an
> English summary. §1, §2 and Day 1–2 are summarised from the PDF's structure: its Thai text
> does not survive text extraction, so check the PDF before quoting those parts.

## 1. Project goal

Build a Backend API for **Project Monitoring**.

## 2. Technical stack

| Layer | Required |
| --- | --- |
| Backend | .NET ≥ 8, Clean Architecture |
| Database | PostgreSQL |
| Infrastructure | Docker & Docker Compose |
| OS | Linux / Unix based |

## 3. Daily tasks

### Day 1 — Database architecture & containerization
* PostgreSQL design for project management: tables `projects`, `tasks`, `users`, `system_logs`, with foreign-key relations and indexing.
* Dockerization: a `Dockerfile` for the API and a `docker-compose.yml` that also runs the PostgreSQL container.
* Task: schema initialises automatically on `docker compose up`.

### Day 2 — Advanced API & business logic
* REST API: CRUD endpoints.
* Complex query: an analytics endpoint using advanced SQL (JOIN, aggregate functions).
* Data export: return results as JSON **and** CSV through the API.

### Day 3 — Linux Operations & Security

> * **Linux Automation:** เขียน **Bash Script** สั่งงานจากภายในหรือภายนอก Container เพื่อ:
>   * ทำ **Database Backup (pg_dump)** และบีบอัดไฟล์เก็บไว้ใน Volume ที่กำหนด
>   * ตรวจสอบสถานะของ Container (Health Check)
> * **System Admin Task:** เขียนคำสั่งเช็ค **Resource Usage** (CPU/Memory) ของ Container ที่รันอยู่
>   * จำลองการจัดการ **Logs** (Log Rotation) เพื่อไม่ให้ไฟล์ Log ขนาดใหญ่เกินไปจนกระทั่ง Disk เต็ม

Summary: Bash scripts, run from inside or outside the container, that
* take a `pg_dump` backup, **compress** it, and keep it in a **designated volume** (R13, ADR-0007);
* check container health (R14);
* show CPU / memory use of the running containers (R15);
* rotate logs so they cannot fill the disk (R16).

## 4. Deliverables (ข้อกำหนดที่ต้องส่ง)

> 1. **GitHub Repository:** ที่มี Source Code ครบถ้วน
> 2. **Docker Setup:** ไฟล์ docker-compose.yml ที่สั่งรันแล้วระบบพร้อมใช้งานทันที (One-command setup)
> 3. **Bash Scripts:** ไฟล์ .sh สำหรับงาน Backup และ Monitoring ตามที่ระบุ
> 4. **API Document:** อาจจะเป็นไฟล์ README.md หรือ Postman Collection

Summary: the full source on GitHub; a compose file that works in one command; `.sh`
scripts for backup and monitoring; API docs as a README **or** a Postman collection
(this repo has both).

## 5. Evaluation (เกณฑ์การประเมิน)

> * **Database Schema (25%):** ความถูกต้องของการออกแบบ Relation และการใช้ PostgreSQL Features
> * **Container Quality (25%):** ความเข้าใจในการจัดการ Docker, Volumes และ Network
> * **Backend Logic (25%):** คุณภาพของโค้ด Node.js และการจัดการ Error Handling
> * **Linux/Unix Skill (25%):** ความสามารถในการใช้ Scripting และคำสั่งระบบเพื่อควบคุม Infrastructure
>
> *ส่งเป็น Link Git Repository (ที่มีไฟล์ README อธิบายการติดตั้ง)*

| Criterion | Weight | What it looks at | Where to look in this repo |
| --- | --- | --- | --- |
| Database schema | 25% | Correct relations, use of PostgreSQL features | `db/init/`, README § Database |
| Container quality | 25% | Handling of Docker, volumes and network | `docker-compose.yml`, `src/Analytics.Api/Dockerfile`, README § Containers |
| Backend logic | 25% | Code quality (the brief says Node.js — see below) and error handling | `src/`, `Api/Errors/GlobalExceptionHandler.cs` |
| Linux/Unix skill | 25% | Scripting and system commands to control the infrastructure | `scripts/`, README § Operations scripts |

Submission: a link to the Git repository, with a README that explains installation.

## Inconsistency in the brief

The Technical Stack section says **.NET ≥ 8 (Clean Architecture)**, but Day 1
("Dockerfile for the Node.js API") and the Backend Logic criterion (คุณภาพของโค้ด Node.js)
mention **Node.js**. This implementation follows the Technical Stack section —
see `open-questions.md` Q1 and ADR-0002.

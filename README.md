# Specimen Check-In

A vertical slice of the IPI Pro specimen receiving workflow: a technician opens a
manifest, checks in each bottle against what the clinic said it shipped, flags what
is missing, and closes the manifest once it reconciles.

Built for the Seven Seas Strategies / NetSoft Full Stack Developer technical assignment.

> **Synthetic data only.** Every lab, patient, and specimen in this repo is invented.

## Stack & choices

| Area | Choice | Why |
| --- | --- | --- |
| Backend | ASP.NET Core 8 (C#), EF Core 8, code-first | Required by the brief; .NET 8 is the current LTS. |
| Database | **SQL Server** (LocalDB on Windows, Docker elsewhere) | Preferred by the brief, and its **Row-Level Security** gives tenant isolation a real database-level backstop that SQLite cannot. |
| Front-end | Vue 3 + Vite + TypeScript + Pinia | Required by the brief; Vite/Pinia is the current Vue 3 default. |
| Tests | xUnit (backend), Vitest + Vue Test Utils (front-end) | Focused on reconciliation logic and tenant isolation. |

Solution layout (Command/Query split kept as separate projects so the write side
and read side can never quietly drift into each other):

```
backend/
├─ SpecimenCheckIn.Models/     entities + API DTOs
├─ SpecimenCheckIn.Context/    DbContext, row-level security, migrations, tenant interceptor
├─ SpecimenCheckIn.Commands/   receive / flag / close        (write side)
├─ SpecimenCheckIn.Queries/    list manifests / get manifest (read side)
├─ SpecimenCheckIn.WebAPI/     controllers, middleware, composition root
└─ SpecimenCheckIn.Tests/      reconciliation, idempotency, tenant isolation
frontend/                       Vue 3 app
```

## Running it locally

Prerequisites: [.NET 8 SDK](https://dotnet.microsoft.com/download) and
[Node.js 20+](https://nodejs.org). Then copy the environment template:

```bash
cp .env.example .env
```

### 1. Database

**Windows** — nothing to install; SQL Server LocalDB ships with Visual Studio and the
default connection string in `.env.example` already points at it.

**macOS / Linux** — start SQL Server in Docker and switch to the container connection
string commented in `.env.example`:

```bash
docker compose up -d
```

### 2. Backend

```bash
cd backend
dotnet run --project SpecimenCheckIn.WebAPI
```

Migrations are applied and the database is seeded on start. The API listens on
<http://localhost:5080>; Swagger UI is at <http://localhost:5080/swagger>.

### 3. Front-end

```bash
cd frontend
npm install
npm run dev
```

The app is served at <http://localhost:5173>.

### Tests

```bash
cd backend && dotnet test          # reconciliation, idempotency, tenant isolation
cd frontend && npm run test        # ManifestDetail component test
```

## Tenant context

Every request carries an `X-Lab-Id` header identifying the current lab. Auth is
deliberately stubbed (the brief does not ask for login/OAuth), but **isolation is
enforced entirely on the server** — see the write-up below. The front-end sends the
lab from `VITE_LAB_ID`; changing it in the UI or in `.env` lets you verify that Lab A
genuinely cannot see Lab B's data.

Seeded labs: `1` = Riverside Clinic Lab, `2` = Northgate Derm Lab.

## Write-up

<!-- Filled in once the slice is complete — see Section 6 of the assignment. -->

### 1. Azure topology

_TODO_

### 2. Tenant isolation

_TODO_

### 3. HIPAA-aware handling

_TODO_

## With more time I would…

_TODO_

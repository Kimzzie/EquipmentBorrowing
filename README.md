# Campus Equipment Borrowing System

A desktop application for laboratory staff to register students and equipment, issue and return equipment (one item or several at once), and review borrowing history. Built with C# / .NET 10, Avalonia UI (MVVM with CommunityToolkit.Mvvm), EF Core and PostgreSQL.

**Authors:** Kimberly Bautista, 2023303116

## Contents

This file is organised by lab. The Lab 4 sections hold the setup guide for the current system, and Labs 5 and 6 describe what changed on top of it.

- [Laboratory Activity 6: automated verification and regression testing](#laboratory-activity-6-automated-verification-and-regression-testing): how to set up and run the tests, how they stay independent, and where the Lab 6 documents and evidence are.
- [Laboratory Activity 5: API integration and acceptance testing](#laboratory-activity-5-api-integration-and-acceptance-testing): the REST API, how to run it, and the acceptance tests.
- [Lab 4 summary and setup guide](#lab-4-summary): prerequisites, database, configuration, architecture (sections 1 to 8).
- [Earlier labs (Labs 1 to 3)](#earlier-labs): the original write-ups, kept as written.

**To run the system:** follow Lab 4 sections 1 to 5 (prerequisites, installation, database, configuration, migrations), then Lab 5 **Setup** and **Run** (start the API first, then the desktop).
**To run the tests:** follow **Test setup** and **Run** in the Lab 6 section.

## Lab 4 summary

Lab 4 moved the system from SQLite to PostgreSQL and added student and equipment management, atomic multi-item checkout, history views and borrowing search. Sections 1 to 8 below describe the current (Lab 4) system. The Lab 1 to 3 write-ups are preserved unchanged under **Earlier labs** at the end.

> **Lab 5 update:** the desktop no longer touches the database. It talks to a new REST API (`src/EquipmentBorrowing.Api`) over HTTP, and the API is the only process that uses PostgreSQL. Sections 1 to 8 below describe the Lab 4 system; where Lab 5 changed how you run it (no migration or seeding at desktop startup, the API must be running first), read **Laboratory Activity 5** near the end of this file first.

> **Lab 6 update:** the system is now covered by automated tests (xUnit against a real PostgreSQL test database) plus two manual acceptance checks. How to set them up and run them is in **Laboratory Activity 6**, and the traceability, execution and defect records are in `docs/lab6-*.md`.

## 1. Prerequisites

- .NET 10 SDK
- PostgreSQL 16 or newer running locally (pgAdmin optional)
- EF Core CLI: `dotnet tool install --global dotnet-ef`
- Git

## 2. Installation

```powershell
git clone https://github.com/Kimzzie/EquipmentBorrowing.git
cd EquipmentBorrowing
dotnet restore
dotnet build
```

## 3. Database setup

In pgAdmin's Query Tool (or `psql`) as a superuser, choose your own password:

```sql
CREATE ROLE equipment_app LOGIN PASSWORD '<choose-a-password>';
CREATE DATABASE equipment_borrowing      OWNER equipment_app;
CREATE DATABASE equipment_borrowing_test OWNER equipment_app;  -- disposable: failure demos only
```

## 4. Configuration (no credentials in Git)

The connection string is never committed. The app reads it, in this order, from:

1. the environment variable `EQUIPMENT_BORROWING_CONNECTION`, or
2. `appsettings.Local.json` in the **solution folder** (git-ignored). The lookup walks upward from the application folder and from the current directory, so one file in the solution folder serves both the app and `dotnet ef`.

To use the file:

```powershell
Copy-Item appsettings.Local.example.json appsettings.Local.json
# then edit appsettings.Local.json and replace <your-password>
```

```json
{
  "ConnectionStrings": {
    "EquipmentBorrowing": "Host=localhost;Port=5432;Database=equipment_borrowing;Username=equipment_app;Password=<your-password>"
  }
}
```

Or, for the current PowerShell session only:

```powershell
$env:EQUIPMENT_BORROWING_CONNECTION = "Host=localhost;Port=5432;Database=equipment_borrowing;Username=equipment_app;Password=<your-password>"
```

## 5. Migrations (create the schema)

```powershell
dotnet ef database update --project src\EquipmentBorrowing.Infrastructure
```

This applies the migrations in `src/EquipmentBorrowing.Infrastructure/Migrations` (`InitialPostgreSql`) to an empty database and creates the whole schema. The application also applies pending migrations at startup. After changing the model, add a new migration:

```powershell
dotnet ef migrations add <Name> --project src\EquipmentBorrowing.Infrastructure
```

## 6. Initial data

On startup, if the Students, Equipment and Borrowings tables are **all empty**, the app seeds 4 students (one restricted) and 6 equipment items (EQ-001 to EQ-006). It never seeds a database that already contains data, so restarting never overwrites anything.

## 7. Run

```powershell
dotnet run --project src\EquipmentBorrowing.Desktop
```

If PostgreSQL is unreachable, the app shows a startup error with a Retry button instead of crashing.

## 8. Architecture and key decisions

```text
View (Avalonia XAML)
  -> ViewModel (CommunityToolkit.Mvvm)
    -> Application Service (use cases, business rules)
      -> Repository interface (Application layer)
        -> EF Core repository (Infrastructure)
          -> PostgreSQL
```

| Project | Responsibility |
|---|---|
| `Domain` | `Student`, `Equipment`, `Borrowing` and their invariants. Depends on nothing. |
| `Application` | One service per use case, repository interfaces, `OperationResult`, typed exceptions. Depends on Domain only. |
| `Infrastructure` | EF Core mappings, migrations, Npgsql repositories, unit of work, seeding. Implements the Application interfaces. |
| `Desktop` | Views, ViewModels and dependency-injection setup. Never runs SQL, configures connections or runs migrations. |
| `ConsoleDemo` | Lab 1 manual-DI demo using the in-memory repositories. |
| `Tests` | Test project (placeholder). Verification for Lab 4 is manual, see `docs/evidence.md`. |

`Infrastructure.AddInfrastructure()` is the only place that knows about Npgsql. The one place a ViewModel touches an Application port directly is startup: `MainWindowViewModel` calls `IDatabaseInitializer` to migrate and seed.

### DbContext and dependency-injection lifetimes

- `DbContext`, repositories, unit of work and Application services are **scoped**.
- ViewModels live as long as the window, so they hold an `IServiceScopeFactory` and open a **new scope for every operation** (load, borrow, return, search). Each operation therefore gets a fresh `DbContext`: it never sees stale tracked data, and a failed operation cannot poison the next one.
- History and search loads are asynchronous and cancellable. A newer selection cancels the older request and its result is discarded, so two students' records never mix on screen.

### Transaction decisions

- Checkout (UC-03 single item, UC-06 several items) and return (UC-05) run inside one explicit **Serializable** transaction. The current student, items and active count are re-read **inside** the transaction before any write (BR-15). Any rejection disposes the transaction without committing, so nothing is saved.
- Two concurrent checkouts of the **same item** are stopped by the partial unique index `UX_Borrowings_OneActivePerEquipment`. Two concurrent checkouts for the **same student** that would together exceed the limit of three are stopped by Serializable isolation: PostgreSQL aborts one of them (SQLSTATE 40001) and the app reports a conflict.
- A failure **before** the commit is a known failure: nothing was saved. A connection lost **during** the commit is an **unknown outcome** (BR-18): the app says so, never claims a rollback, never retries automatically and refreshes the data for staff to check.
- Automatic retry (`EnableRetryOnFailure`) is deliberately **not** enabled, for the same reason.

### Database integrity (BR-01, 05, 10, 14, 16)

Primary keys; foreign keys with `Restrict` (no hard delete, history is preserved); required-field and not-blank check constraints; `ExpectedReturnDate > DateBorrowed`; a status/return-date consistency check; unique indexes on `lower(btrim(StudentNumber))` and `lower(btrim(EquipmentCode))` through stored generated columns, so database uniqueness matches the application's normalization; and the single-active-borrowing partial unique index.

### Dates

Dates are `DateOnly`, taken from `TimeProvider` in the machine's local time zone.

## Safe failure demonstration

Use the **disposable** database only:

```powershell
$env:EQUIPMENT_BORROWING_CONNECTION = "Host=localhost;Port=5432;Database=equipment_borrowing_test;Username=equipment_app;Password=<your-password>"
$env:EQUIPMENT_BORROWING_FAULT = "BeforeCommit"   # or "AfterCommit"
dotnet run --project src\EquipmentBorrowing.Desktop
```

`EQUIPMENT_BORROWING_FAULT` is a test seam (`FaultInjection.cs`). It refuses to act unless the connected database name ends in `_test`. `BeforeCommit` fails after the writes were sent but before the commit (rolled back). `AfterCommit` commits and then simulates a lost acknowledgment (unknown outcome). Remove the variables afterwards:

```powershell
Remove-Item Env:EQUIPMENT_BORROWING_FAULT
Remove-Item Env:EQUIPMENT_BORROWING_CONNECTION
```

## Documentation

- `docs/use-case-diagram.png`: use-case diagram (primary actor: Laboratory Staff / Equipment Custodian; supporting system: PostgreSQL)
- `docs/use-case-catalog.md`: catalog and Lab 4 classification
- `docs/traceability.md`: use cases mapped to implementation and acceptance IDs
- `docs/evidence.md`: expected versus observed results, A01 to A19
- `docs/database-diagram.png`, `docs/database-queries.sql`: from earlier labs

---

# Laboratory Activity 5: API integration and acceptance testing

**Author:** Kimberly Bautista, 2023303116 (individual). Tag: `lab5-submitted`. Lab 4 baseline: tag `lab4-submitted` (commit `d23dd40`); see `docs/baseline-record.md`.

Lab 5 splits the system into two processes. The new **API** (`src/EquipmentBorrowing.Api`, ASP.NET Core controllers) exposes the Lab 4 use cases as a REST service under `/api/v1`. The **desktop** is now only an HTTP client. The Lab 4 business rules (BR-01 to BR-19) were not rewritten: the API calls the same Application services.

```text
Avalonia View -> ViewModel -> IEquipmentApi / EquipmentApiClient (HttpClient)
                                      | HTTP, JSON
                                      v
                  Api (controllers, error mapping, OpenAPI)
                                      v
                  Application services (use cases, business rules)
                                      v
                  Repository interfaces -> Infrastructure (EF Core, Npgsql) -> PostgreSQL
```

| Project | Lab 5 responsibility |
|---|---|
| `Desktop` | Views, view models, `Api/EquipmentApiClient.cs`. No project references and no database packages. |
| `Api` | Controllers `Students`, `Equipment`, `Borrowings`, `CheckoutRequests`; DTO contracts; maps application results to HTTP status and `{code,message}`; OpenAPI document. |
| `Application`, `Domain` | Business rules and use cases, unchanged apart from three small read services (`Get*Service`) and one query method. |
| `Infrastructure` | EF Core, Npgsql, migrations, repositories. Used only by the API (and the test-data tool). |
| `tools/EquipmentBorrowing.TestData` | `reset` and `snapshot` commands for the disposable test database. |

## Setup

1. Prerequisites as in section 1 (.NET 10 SDK, PostgreSQL 16 or newer, `dotnet-ef`, Git).
2. Create the databases. The role must **own** the database, otherwise PostgreSQL 15 and newer refuse to create tables in the `public` schema:

```sql
CREATE ROLE equipment_app LOGIN PASSWORD '<choose-a-password>';
CREATE DATABASE equipment_borrowing          OWNER equipment_app;
CREATE DATABASE equipment_borrowing_lab5_test OWNER equipment_app;  -- disposable: acceptance tests only
```

3. Configure the connection (never committed). The API and the tools read `EQUIPMENT_BORROWING_CONNECTION` first, then the git-ignored `appsettings.Local.json` in the solution folder (copy `appsettings.Local.example.json` and replace `<your-password>`; the file is found by walking up from the app folder). For one PowerShell window, use single quotes:

```powershell
$env:EQUIPMENT_BORROWING_CONNECTION = 'Host=localhost;Port=5432;Database=equipment_borrowing;Username=equipment_app;Password=<your-password>'
```

   The variable only lives in the window where you set it, so set it again in every window that starts the API or a tool. Use a password that is not guessable from this repository (do not reuse a student number).

4. Create the schema. Lab 5 added **no** migration (the only one is `InitialPostgreSql`), and the API does **not** migrate on startup:

```powershell
dotnet ef database update --project src\EquipmentBorrowing.Infrastructure
```

   A fresh database is empty (the Lab 4 startup seeding ran in the desktop and was removed). Register students and equipment through the app or the API, or use the test database below.

## Run

```powershell
dotnet run --project src\EquipmentBorrowing.Api --launch-profile http     # API at http://localhost:5080
dotnet run --project src\EquipmentBorrowing.Desktop                       # in a second window
```

- API base URL: `http://localhost:5080/` by default. The desktop reads `EQUIPMENT_BORROWING_API_URL` if you need another address. The desktop needs **no** database setting.
- OpenAPI document: `http://localhost:5080/openapi/v1.json`.
- Run one API at a time (port 5080), and stop it before `dotnet build`, because a running API locks its DLLs.

### Endpoints (all under `/api/v1`)

| ID | Method and route | Purpose | Success |
|---|---|---|---|
| API-01 | GET `/equipment?available=true` | List equipment | 200 |
| API-02 | GET `/students?search=` | List students | 200 |
| API-03 | GET `/students/{id}` | One student | 200 |
| API-04 | POST `/students` | Register a student (starts eligible) | 201 + `Location` |
| API-05 | PUT `/students/{id}` | Edit a student | 200 |
| API-06 | PATCH `/students/{id}/eligibility` | Permit or restrict | 200 |
| API-07 | GET `/equipment/{id}` | One item | 200 |
| API-08 | POST `/equipment` | Register an item (starts available) | 201 + `Location` |
| API-09 | PUT `/equipment/{id}` | Edit descriptive fields | 200 |
| API-10 | POST `/borrowings` | Issue one item | 201 + `Location` |
| API-11 | POST `/checkout-requests` | Issue two or more items, all or nothing | 201 |
| API-12 | POST `/borrowings/{id}/return` | Return (the server sets the date) | 200 |
| API-13 | GET `/borrowings/{id}` | One borrowing | 200 |
| API-14 | GET `/borrowings?search=&status=&sort=&direction=` | Search, filter, sort | 200 |
| API-15 | GET `/students/{id}/borrowings` | Student history | 200 |
| API-16 | GET `/equipment/{id}/borrowings` | Item history | 200 |

Errors always have the body `{ "code": "...", "message": "..." }`. Status mapping: 400 validation (`validation_failed`), 404 missing (`not_found`, `route_not_found`), 405 `method_not_allowed`, 409 business rule or duplicate (`rule_violation`, `duplicate`, `conflict`), 500 safe server failure (`database_error`, `commit_outcome_unknown`, `internal_error`). The body never contains credentials, connection strings or stack traces.

## Key design decisions

- **Dependency direction.** The desktop depends on nothing but HTTP; the API depends on Application and Infrastructure. `docs/acceptance/Check-DesktopDependencies.ps1` proves the desktop has no project references, no database packages and no database assemblies in its output.
- **Unreachable versus server error.** An unreachable API produces no HTTP response, so the client reports "cannot reach the service". A reachable API that answers 500 is reported as a server error. The two are different messages and different code paths (`ApiResult` with an `ApiFailure`).
- **Unknown commit outcome.** If the connection is lost after COMMIT was sent, the API answers 500 `commit_outcome_unknown`, never retries, and tells staff to refresh and check before trying again. A failure status is not proof of rollback.
- **Writes are not cancelled by a client disconnect.** Write requests use `CancellationToken.None` so an abandoned request cannot abort a commit that is already running; reads use the request token.
- **Atomic checkout.** Single and multi-item checkout and return run in one Serializable transaction with the rules re-checked inside it (unchanged from Lab 4).
- **Server decides dates.** `expectedReturnDate` must be later than the server's local date; borrow and return dates are set by the server.

## Acceptance tests (T01 to T08)

The tests run against the **disposable** database only. `reset` and `snapshot` refuse any database whose name does not end in `_test`.

```powershell
# Window A: API with the fixed test clock (2026-11-10) against the test database
$env:EQUIPMENT_BORROWING_CONNECTION = 'Host=localhost;Port=5432;Database=equipment_borrowing_lab5_test;Username=equipment_app;Password=<your-password>'
dotnet run --project src\EquipmentBorrowing.Api --launch-profile http-test-clock

# Window B: reset, run a test, read the database independently
dotnet run --project tools\EquipmentBorrowing.TestData -- reset
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass     # this window only, if scripts are blocked
.\docs\acceptance\Invoke-Acceptance.ps1 -Test T01             # T01 .. T06, T05-fault, AfterRestart
dotnet run --project tools\EquipmentBorrowing.TestData -- snapshot
```

- `T05-fault` needs the API started with `$env:EQUIPMENT_BORROWING_FAULT = 'BeforeCommit'`; the unknown-commit check in T08 uses `'AfterCommit'`. Remove the variable afterwards (`Remove-Item Env:EQUIPMENT_BORROWING_FAULT`) and restart the API. The fault switch refuses to act unless the database name ends in `_test`.
- `AfterRestart` is run after stopping and restarting the API and compares the database with the snapshot saved by the last write test.
- T07 (the desktop is an HTTP client) and T08 (unreachable API versus 500, recovery) combine the scripts with manual steps; the steps and what was observed are in `docs/acceptance/test-results.md`.

## Lab 5 documents and evidence

- `docs/acceptance/test-results.md`: the consolidated test record (T01 to T08, honest results, issues found and corrected).
- `docs/evidence/lab5/`: the script-generated evidence files, `T07-*` and `T08-*` captures, and `screenshots/`.
- `docs/lab5-traceability.md`: Lab 4 use cases mapped to endpoints, code and evidence.
- `docs/baseline-record.md`: the Lab 4 baseline, the before and after boundary, and the justified refactoring.
- `docs/acceptance/*.ps1`, `tools/EquipmentBorrowing.TestData`: the repeatable test tooling.

---

# Laboratory Activity 6: automated verification and regression testing

Lab 6 adds automated checks for the system built in Labs 1 to 5. It adds no new business module. The Lab 4 use-case
suite and the Lab 5 API reference stay authoritative. The tests live in one xUnit project, `tests/EquipmentBorrowing.Tests`.

## What is tested

- **22 automated scenarios** from the Lab 6 test case suite: `TC6-U01` to `U09` and `U12` to `U16` (use cases) and `TC6-E01` to `E08`
  (edge cases and failures). Each test carries a trait such as `[Trait("TC6", "U01")]`.
- **The real thing, not mocks.** API tests host the actual ASP.NET Core pipeline in-process with `WebApplicationFactory<Program>`
  and talk to a real PostgreSQL test database. Constraint, persistence and transaction claims are always checked through a **fresh,
  independent PostgreSQL connection** (pooling off), never through the API's own context.
- **Two manual acceptance checks**, `MA6-01` (desktop responsiveness) and `MA6-02` (independent API and synchronization), recorded in `docs/lab6-manual-checks.md`.

## Test setup (one time)

1. Create the disposable database (the same one as in Lab 4). The tests apply the migrations themselves on the first reset.

   ```sql
   CREATE DATABASE equipment_borrowing_test OWNER equipment_app;   -- disposable: tests only
   ```

2. Point the tests at it for the current PowerShell window (single quotes, never committed):

   ```powershell
   $env:EQUIPMENT_BORROWING_CONNECTION = 'Host=localhost;Port=5432;Database=equipment_borrowing_test;Username=equipment_app;Password=<your-password>'
   ```

   **Safety:** the tests read **only** this variable. They never fall back to `appsettings.Local.json` (which might point at real data),
   and they refuse to start unless the database name ends in `_test`, because every test wipes all rows. Do not use SQLite or EF Core InMemory
   to check these behaviours; they cannot prove PostgreSQL constraints or rollback. No Docker or paid service is needed.

## Run

Close any API or desktop you started by hand first: a running API locks the build output.

```powershell
dotnet test tests\EquipmentBorrowing.Tests                       # everything (157 tests, about one minute)
dotnet test tests\EquipmentBorrowing.Tests --filter "TC6=U09"    # one scenario
dotnet test tests\EquipmentBorrowing.Tests --filter "TC6~E07"    # E07a, E07b and E07c together
```

Expected result on 2026-11-28: **157 executed, 157 passed, 0 failed.** 153 map to TC6 cases and 4 are `InfrastructureSmokeTests`
that check the harness itself.

## How the tests stay independent and repeatable

- **Reset before every test.** `TestDatabase.ResetAsync()` truncates the tables (`RESTART IDENTITY`) and inserts the fixture S1 to S4 and E1 to E7.
  No test depends on another, and the order does not matter.
- **Fixed date D = 2026-11-10.** The test host sets `EQUIPMENT_BORROWING_FIXED_DATE`, so dates such as "returned on D" are deterministic.
- **Serial execution.** All tests share one database and one process-wide fault switch, so parallelism is disabled for the assembly.
- **IDs come from the API.** Tests resolve students and equipment through the HTTP API instead of hard-coding database keys.
- **Failures are provoked on purpose.** `FaultScope` arms the existing `EQUIPMENT_BORROWING_FAULT` seam only for the length of one test:
  `BeforeCommit` fails after the writes were sent (rollback, `E04`), `AfterCommit` commits and then loses the acknowledgement (unknown outcome, `E07c`).
- **Restart check (`E08`).** `ApiProcess` runs the actual API as a separate process, stops it, starts it again and compares the records
  and the database snapshot. This is the documented integration harness, so no manual restart is needed.
- **The real desktop client is under test.** The unmodified client sources from `EquipmentBorrowing.Desktop/Api` are compiled into the test project
  by linked includes (no Avalonia needed), so `E07` exercises the exact code the desktop uses.

## What the test doubles can and cannot prove

| Double | Proves | Does not prove |
|---|---|---|
| `ThrowingHandler`, `HangingHandler` | The desktop client reports "no HTTP response" as `Unreachable`, does not retry, and handles a timeout | Real network behaviour, or anything about the server |
| `CountingHandler` | How many requests the client really sent (no automatic retry) | Nothing about the database |
| `LoseResponseHandler` | The client warns "may or may not have been processed" while the data really is saved | Real network behaviour |

All database claims (rollback, constraints, unknown outcome, persistence) use genuine PostgreSQL and fresh connections.
`E07a` also uses a real closed port, so one "unreachable" check involves no double at all.

## Manual checks

`MA6-01` and `MA6-02` need a person, by design (the handout asks for no UI automation). The steps, the observations and the screenshot gallery are in
`docs/lab6-manual-checks.md`. The helper `docs/acceptance/Start-SlowProxy.ps1` delays API responses so the loading state can be observed.

## Defect found and fixed

`D-1`: a `415 Unsupported Media Type` response used `application/problem+json` instead of the API's `{ code, message }` error body.
Found by `TC6-E03`, fixed with one line in `Program.cs` (`SuppressMapClientErrors`), and retested: the failing test passes and the full suite is green.
Details are in `docs/lab6-execution-and-defects.md`. No defect is unresolved.

## Documented specification ambiguity

The contract says a stable borrowing ID breaks date ties but not whether the tie-break follows the sort direction. The implementation orders
ties by ID in the same direction as the date, and `TC6-U09` asserts exactly that rule. The specification was not changed.

## Lab 6 documents and evidence

- `docs/lab6-traceability.md`: baseline record, scenario index, UC to BR to API to TC6 matrix, API coverage.
- `docs/lab6-execution-and-defects.md`: test execution record and the defect / regression record.
- `docs/lab6-manual-checks.md`: `MA6-01` and `MA6-02` with observations and screenshots.
- `docs/evidence/lab6/`: run summary and raw results (`test-run-2026-11-28.txt`, `lab6-final-trx.xml`), `D-1` evidence, dependency checks, OpenAPI capture and `screenshots/`.

## Baseline and revision

The Lab 5 baseline is the tag `lab5-submitted` (commit `a1ab4464575c39e72c2585494b979ba52c6c5d45`). The final Lab 6 revision is the tag `lab6-submitted`.

# Earlier labs

The sections below are the original Lab 1 to 3 documentation, kept as written. Where they mention SQLite, that was replaced by PostgreSQL in Lab 4 (see above).


## 1. Solution Structure

- **Domain** — Contains the core business concepts and their own rules: `Student`, `Equipment`, `Borrowing`, and `BorrowingStatus`. These classes manage only their own internal state (e.g., `Equipment.MarkAsBorrowed()`) and have no knowledge of databases, files, or the application's use cases.
- **Application** — Contains the use case logic (`BorrowEquipmentService`) and the repository interfaces (`IStudentRepository`, `IEquipmentRepository`, `IBorrowingRepository`) that use cases depend on. This layer coordinates Domain objects but never implements storage itself.
- **Infrastructure** — Contains the concrete implementations of the repository interfaces. Currently this is `InMemoryStudentRepository`, `InMemoryEquipmentRepository`, and `InMemoryBorrowingRepository`, which store data in C# collections instead of a real database.
- **Tests** — Contains the automated test project for verifying Application/Domain behavior.
- **ConsoleDemo** — A runnable console application that wires everything together manually and demonstrates one successful and two failed borrowing attempts.

## 2. Dependency Direction

```text
   ConsoleDemo (executable / future UI)
          │
          ▼
     Application
       │      ▲
       ▼      │
     Domain   │
              │
     Infrastructure
```

- `ConsoleDemo` depends on `Application`, `Domain`, and `Infrastructure` (it needs to construct concrete repositories and pass them into the service).
- `Application` depends only on `Domain`. It defines repository *interfaces* but does not depend on their implementations.
- `Infrastructure` depends on `Application` (to implement its interfaces) and `Domain` (to work with entities).
- `Domain` depends on nothing else in the solution — it is the innermost, most stable layer.

## 3. Use Case Mapping

```text
Actor: Student
Use Case: Borrow Equipment
Application Service: BorrowEquipmentService.ExecuteAsync
Domain Objects Used: Student, Equipment, Borrowing, BorrowingStatus
Repository Interfaces Used: IStudentRepository, IEquipmentRepository, IBorrowingRepository
Infrastructure Implementations Used: InMemoryStudentRepository, InMemoryEquipmentRepository, InMemoryBorrowingRepository
```

## 4. Reflection

**1. Why should the application service depend on a repository interface instead of directly depending on a database implementation?**

Depending on an interface means `BorrowEquipmentService` doesn't need to know or care how data is actually stored. It can be tested with fake/in-memory data, and the real storage mechanism (SQLite, PostgreSQL, etc.) can be swapped in later without changing the service's code at all — only a new class implementing the same interface is needed.

**2. Which parts of your current solution could remain unchanged if SQLite were added later?**

The entire `Domain` and `Application` projects — including `BorrowEquipmentService` and all three repository interfaces — would remain unchanged. Only `Infrastructure` would change, by adding new classes like `SqliteEquipmentRepository` that implement the existing interfaces using Entity Framework Core instead of a `List<T>`.

**3. Which project would eventually contain Avalonia Views?**

A new UI project (similar in role to `ConsoleDemo`, but built with Avalonia) would contain the Views. It would sit at the same outer layer — referencing `Application` and `Infrastructure` — but `Domain` and `Application` would never reference it back.

**4. Should an Avalonia button directly execute database queries? Why or why not?**

No. A button's click handler should call an Application service method (like `BorrowEquipmentService.ExecuteAsync`), the same way `ConsoleDemo` does. If the UI executed SQL directly, business rules like "max active borrowings" would either be duplicated in the UI layer or skipped entirely, and the logic couldn't be reused or tested independently of the UI.

**5. What part of your implementation represents the actual business operation requested by the actor?**

`BorrowEquipmentService.ExecuteAsync` is the business operation itself — it validates all the rules from Part A (student exists, is eligible, equipment exists and is available, max borrowings not exceeded) and only creates a `Borrowing` record if every rule passes. Everything else in the solution exists to support this one operation.



---

## Laboratory Activity 2 — Avalonia UI and MVVM

### 1. Desktop Project

`EquipmentBorrowing.Desktop` is the presentation layer added in Lab 2. It contains Avalonia Views (XAML), ViewModels (CommunityToolkit.Mvvm), and the application's composition root (`App.axaml.cs`). It references `Application` and `Infrastructure` to compose the dependency graph, but `Domain` and `Application` have zero references back to it or to Avalonia — the business rules built in Lab 1 didn't change at all.

### 2. Updated Architecture

```text
Avalonia View (EquipmentView / BorrowingsView)
      │
      │ Binding / Command
      ▼
ViewModel (EquipmentViewModel / BorrowingsViewModel)
      │
      │ Application Operation
      ▼
Application Service (BorrowEquipmentService / ReturnEquipmentService)
      │
      ├──────────► Domain (Student, Equipment, Borrowing, BorrowingStatus)
      │
      ▼
Repository Interface (IStudentRepository, IEquipmentRepository, IBorrowingRepository)
      ▲
      │
Infrastructure Implementation (InMemoryStudentRepository, InMemoryEquipmentRepository, InMemoryBorrowingRepository)
```

### 3. Borrow Equipment Flow

The user selects a student, equipment, and return date in `EquipmentView`, then clicks "Borrow Equipment," which triggers `EquipmentViewModel.BorrowCommand`. The ViewModel first runs presentation validation (are a student and equipment selected, is the date valid?). If that passes, it calls `BorrowEquipmentService.ExecuteAsync(...)`, which checks the actual business rules — student eligibility, equipment availability, and the active-borrowing limit — against the repositories. The result (`BorrowResult`) comes back to the ViewModel, which sets `StatusMessage` and reloads the equipment list so the UI reflects the new availability state.

### 4. Return Equipment Flow

The user selects an active borrowing in `BorrowingsView` and clicks "Return Equipment," triggering `BorrowingsViewModel.ReturnCommand`. The ViewModel calls `ReturnEquipmentService.ExecuteAsync(borrowingId)`, which looks up the borrowing, confirms it hasn't already been returned, marks the borrowing and its equipment as returned, and persists the change. The result comes back to the ViewModel, which updates `StatusMessage` and reloads the active borrowings list.

### 5. Architectural Reflection

**Why should the View not call a repository directly?**

The View exists only to display data and forward user actions. If it called a repository directly, it would need to know about borrowing rules and data access details that have nothing to do with rendering XAML, and those rules would end up duplicated or bypassed outside the Application layer.

**Why should business rules not be implemented in the ViewModel?**

The ViewModel's job is to translate between the View and the Application layer — collecting input, holding presentation state, and reporting results. Putting rules like "equipment must be available" in the ViewModel would duplicate logic that already lives in `BorrowEquipmentService`/`ReturnEquipmentService`, and any future UI (or automated test) would have to reimplement it.

**What is the responsibility of the ViewModel?**

To hold presentation state (selected items, status messages, observable collections), expose commands the View can bind to, run lightweight presentation validation, and call the appropriate Application service — nothing more.

**Why can the existing Application layer work without knowing that Avalonia is being used?**

`BorrowEquipmentService` and `ReturnEquipmentService` depend only on repository interfaces and Domain types. They have no reference to Avalonia at all, so from their point of view, a button click and a console `Console.ReadLine()` call look identical — both are just a method call with some arguments.

**What advantage is gained from registering dependencies in one composition point?**

`App.axaml.cs` is the only place in the entire solution that knows the concrete implementations (`InMemoryStudentRepository`, etc.). Every other class only depends on interfaces. Changing an implementation, or swapping in test doubles, means editing one method instead of hunting through every ViewModel and service.

**If the in-memory repository were replaced by SQLite later, which parts of the current interface should remain largely unchanged?**

All of `Domain`, `Application` (interfaces and services), and essentially all of `Desktop` — the Views, ViewModels, and navigation logic — would stay the same. Only the three `InMemory*Repository` classes in `Infrastructure` would be replaced with EF Core-based equivalents, and the one line registering them in `App.axaml.cs` would change.





---

## Laboratory Activity 3 — From In-Memory Data to Persistent Storage

### 1. Relational Database Design

The schema has 3 tables — see `docs/database-diagram.png` for the full ER diagram.

- **Students**: `Id` (PK), `StudentNumber` (unique), `Name`, `IsAllowedToBorrow`
- **Equipment**: `Id` (PK), `Name`, `Type`, `Description` (nullable), `IsAvailable`
- **Borrowings**: `Id` (PK), `StudentId` (FK → Students), `EquipmentId` (FK → Equipment), `DateBorrowed`, `ExpectedReturnDate`, `DateReturned` (nullable), `Status`

Both foreign keys use `ON DELETE RESTRICT`, so a student or equipment record with borrowing history can't be deleted. A filtered unique index on `Borrowings.EquipmentId` (where `Status = 'Active'`) enforces "one active borrowing per item" at the database level, not just in application code.

### 2. SQLite and EF Core

Four NuGet packages were added to `EquipmentBorrowing.Infrastructure`: `Microsoft.EntityFrameworkCore`, `.Sqlite`, `.Design`, and `.Tools`. The `dotnet-ef` CLI tool was installed globally to generate and apply migrations. Since `EquipmentBorrowingDbContext` lives in Infrastructure (a class library with no startup/host project of its own), an `EquipmentBorrowingDbContextFactory` implementing `IDesignTimeDbContextFactory<T>` was added so `dotnet ef` commands can construct the context without needing `--project`/`--startup-project` flags.

### 3. DbContext

`EquipmentBorrowingDbContext` is the single point of contact between the application and SQLite. It exposes one `DbSet<T>` per entity (`Students`, `Equipment`, `Borrowings`) and applies every `IEntityTypeConfiguration<T>` in the assembly automatically via `ApplyConfigurationsFromAssembly`, so adding a new entity configuration never requires editing the context itself.

### 4. Repository Transition

Before:
```text
IEquipmentRepository → InMemoryEquipmentRepository (List<Equipment>)
```

After:
```text
IEquipmentRepository → EfEquipmentRepository → EquipmentBorrowingDbContext → SQLite
```

The interfaces stayed almost identical — `BorrowEquipmentService` and `ReturnEquipmentService` didn't change their dependencies at all. Two additions were needed: `IEquipmentRepository.UpdateAsync` (the in-memory version didn't need it, since mutating the shared object was enough — SQLite requires an explicit save), and a small `IUnitOfWork` interface, so the equipment update and the new borrowing record commit in one transaction instead of two independent ones.

### 5. Migration Process

```powershell
cd src\EquipmentBorrowing.Infrastructure
dotnet ef migrations add InitialCreate --output-dir Data/Migrations
dotnet ef database update
```

The app also calls `context.Database.Migrate()` on every startup (in `App.axaml.cs`), so a fresh clone of this repository doesn't require the manual `dotnet ef` step — the database is created and brought up to date automatically the first time the app runs.

### 6. Generated SQL

**LINQ Query 1 (filter) — available equipment**
```csharp
await _context.Equipment.AsNoTracking().Where(e => e.IsAvailable).ToListAsync();
```
Generated SQL:
```sql
SELECT "e"."Id", "e"."Description", "e"."IsAvailable", "e"."Name", "e"."Type"
FROM "Equipment" AS "e"
WHERE "e"."IsAvailable"
```
Explanation: SQLite stores `bool` as an integer, but EF's generated `WHERE` clause treats it directly as truthy — no `= 1` needed.

**LINQ Query 2 (join) — active borrowings with student and equipment names**
```csharp
from b in _context.Borrowings.AsNoTracking()
join s in _context.Students.AsNoTracking() on b.StudentId equals s.Id
join e in _context.Equipment.AsNoTracking() on b.EquipmentId equals e.Id
where b.Status == BorrowingStatus.Active
select new ActiveBorrowingSummary(b.Id, s.Name, e.Name, b.DateBorrowed, b.ExpectedReturnDate);
```
Generated SQL:
```sql
SELECT "b"."Id", "s"."Name", "e"."Name", "b"."DateBorrowed", "b"."ExpectedReturnDate"
FROM "Borrowings" AS "b"
INNER JOIN "Students" AS "s" ON "b"."StudentId" = "s"."Id"
INNER JOIN "Equipment" AS "e" ON "b"."EquipmentId" = "e"."Id"
WHERE "b"."Status" = 'Active'
```
Explanation: one round trip does what used to take a query per borrowing plus two lookups per row — a single `SELECT` with two `INNER JOIN`s.

**LINQ Query 3 (aggregate, bonus) — active borrowing count for a student**
```csharp
await _context.Borrowings.AsNoTracking()
    .CountAsync(b => b.StudentId == studentId && b.Status == BorrowingStatus.Active);
```
Generated SQL:
```sql
SELECT COUNT(*)
FROM "Borrowings" AS "b"
WHERE "b"."StudentId" = @studentId AND "b"."Status" = 'Active'
```
Explanation: `studentId` is passed as a parameter (`@studentId`), not concatenated into the query text — this is EF's default behavior and is what keeps LINQ queries safe from SQL injection.

**Tracking decisions:** every list/filter query above uses `AsNoTracking()`, since the results are only ever displayed. `Equipment.GetByIdAsync` and `Borrowing.GetByIdAsync`, by contrast, stay tracked — both are called immediately before mutating the entity (`MarkAsBorrowed()`, `MarkAsReturned()`), so EF's change tracker needs to see the change to write it back on `SaveChangesAsync`.

### 7. Persistence Demonstration

We ran the full test from Part K: started the app, borrowed a piece of equipment, confirmed it appeared in the Borrowings tab, closed the application completely, reopened it, and confirmed the borrowing was still there. We then returned it, closed and reopened the app again, and confirmed the returned state was preserved and the equipment showed as available again.

### 8. Architectural Reflection

**1. Why did the application not need to be completely rewritten when SQLite was introduced?**

Domain and Application never referenced any storage mechanism — only repository interfaces. Only Infrastructure (three new `Ef*Repository` classes) and the DI registrations in `App.axaml.cs` changed.

**2. Why should the ViewModel not use `DbContext` directly?**

`DbContext` exposes low-level persistence concerns — change tracking, raw table access, connection lifetime. A ViewModel's job is presentation state and calling application services; coupling it to `DbContext` would leak EF/SQL details into the UI layer and make the provider impossible to swap or test in isolation.

**3. What responsibility does the repository implementation now perform?**

It translates interface calls into real LINQ queries against `DbContext`, decides whether each query should track or not, and stages inserts/updates so `IUnitOfWork.SaveChangesAsync()` can commit them together.

**4. What is the purpose of an EF Core migration?**

A versioned, reproducible description of schema changes, generated from the C# model, so the database structure can be created and evolved consistently across machines without hand-written SQL DDL.

**5. Why are foreign keys important in the borrowing database?**

They enforce referential integrity at the database level — every `Borrowing.StudentId`/`EquipmentId` is guaranteed to point to a real row, and `ON DELETE RESTRICT` stops a student or equipment record from being deleted while borrowing history references it.

**6. Why can a read-only query benefit from `AsNoTracking()`?**

EF skips building change-tracking snapshots for the returned entities, which is faster and uses less memory, since there's no intention of ever calling `SaveChanges()` on that data.

**7. What would happen to the rest of the application if the SQLite implementation were replaced later by another database provider?**

Only Infrastructure would change — swap `UseSqlite` for the new provider's equivalent and adjust the connection string. Domain, Application, and Desktop would be completely unaffected, since none of them reference SQLite or EF Core directly.
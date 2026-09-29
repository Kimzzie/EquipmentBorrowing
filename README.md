# Equipment Borrowing System

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
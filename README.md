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
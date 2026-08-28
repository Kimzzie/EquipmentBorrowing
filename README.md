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
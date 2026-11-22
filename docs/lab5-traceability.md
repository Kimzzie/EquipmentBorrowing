# Lab 5 Traceability: Lab 4 use cases to API endpoints, code and evidence

Author: Kimberly Bautista, 2023303116 (individual).

This extends `docs/traceability.md` (Lab 4), which is kept unchanged. The Lab 4 use cases (UC-01 to UC-09 and UC-12 to UC-16) and business rules (BR-01 to BR-19) are the authoritative functional specification; Lab 5 exposes the same actor goals through the REST API under `/api/v1` and migrates the Avalonia client to HTTP.

How to read the implementation columns: **API** is the controller class in `src/EquipmentBorrowing.Api/Controllers`, **Application** is the use-case service in `src/EquipmentBorrowing.Application/Services` (the Lab 4 business rules live there and were not rewritten), and **Desktop** is the view model in `src/EquipmentBorrowing.Desktop/ViewModels` that calls `Api/EquipmentApiClient.cs`. Evidence files are in `docs/evidence/lab5/`.

| Lab 4 use case | Rules | Endpoint | Test | API controller | Application service | Desktop view model | Evidence |
|---|---|---|---|---|---|---|---|
| UC-01 View equipment | BR-05 to 07, 10, 18, 19 | API-01 | T01, T07 | `EquipmentController` | `FindEquipmentService`, `GetEquipmentService` | `EquipmentViewModel` | `T01-20261121-132320.md`, `T07-snapshot.txt` |
| UC-02 View available equipment | BR-10, 18, 19 | API-01 (`available=true`) | T01, T07 | `EquipmentController` | `FindEquipmentService`, `GetEquipmentService` | `EquipmentViewModel` | `T01-20261121-132320.md` (step 2) |
| UC-03 Borrow one item | BR-08 to 11, 14 to 16, 18, 19 | API-10, API-13 | T04, T05, T07 | `BorrowingsController` | `BorrowEquipmentService`, `GetBorrowingService` | `EquipmentViewModel` | `T04-20261121-132630.md`, `T05-20261121-132721.md`, `T05-fault-20261121-133737.md`, `T08-snapshot-after-restart.txt` |
| UC-04 View active borrowings | BR-14, 18, 19 | API-14 (`status=Active`) | T01, T06, T07 | `BorrowingsController` | `SearchBorrowingsService`, `GetBorrowingService` | `BorrowingsViewModel` | `T01-20261121-132320.md`, `T06-20261121-134007.md` |
| UC-05 Return equipment | BR-10, 13 to 16, 18, 19 | API-12, API-13 | T04, T07 | `BorrowingsController` | `ReturnEquipmentService`, `GetBorrowingService` | `BorrowingsViewModel` | `T04-20261121-132630.md`, `T07-snapshot.txt` |
| UC-06 Borrow several items at once | BR-08 to 12, 14 to 16, 18, 19 | API-11 | T05, T07 | `CheckoutRequestsController` | `CheckOutMultipleEquipmentService`, `GetBorrowingService` | `EquipmentViewModel` | `T05-20261121-132721.md`, `T05-20261121-134234.md`, `T05-fault-20261121-133737.md` |
| UC-07 View a student's history | BR-14, 18, 19 | API-02, 03, 15 | T01, T06, T07 | `StudentsController` | `FindStudentsService`, `GetStudentService`, `ViewStudentHistoryService` | `HistoryViewModel` | `T06-20261121-134007.md`, `screenshots/T07-04-ui-history-student.png` |
| UC-08 View an item's history | BR-14, 18, 19 | API-01, 07, 16 | T01, T06, T07 | `EquipmentController` | `FindEquipmentService`, `GetEquipmentService`, `ViewEquipmentHistoryService` | `HistoryViewModel` | `T06-20261121-134007.md`, `screenshots/T07-05-ui-history-equipment.png` |
| UC-09 Search borrowings | BR-17 to 19 | API-14 | T06, T07 | `BorrowingsController` | `SearchBorrowingsService` | `BorrowingSearchViewModel` | `T06-20261121-134007.md` |
| UC-12 Register a student | BR-01, 02, 16, 18, 19 | API-04 | T02, T07 | `StudentsController` | `RegisterStudentService` | `StudentsViewModel` | `T02-20261121-132520.md`, `T07-snapshot.txt` (ST-500) |
| UC-13 Edit a student | BR-01 to 03, 14, 16, 18, 19 | API-02, 03, 05 | T02, T07 | `StudentsController` | `FindStudentsService`, `GetStudentService`, `UpdateStudentService` | `StudentsViewModel` | `T02-20261121-132520.md` |
| UC-14 Permit or restrict a student | BR-04, 08, 13, 14, 18, 19 | API-02, 03, 06 | T02, T07 | `StudentsController` | `FindStudentsService`, `GetStudentService`, `ChangeStudentEligibilityService` | `StudentsViewModel` | `T02-20261121-132520.md`, `screenshots/T07-02-ui-students-restricted-after-patch.png`, `T07-snapshot.txt` |
| UC-15 Register equipment | BR-05, 06, 10, 16, 18, 19 | API-08 | T03, T07 | `EquipmentController` | `RegisterEquipmentService` | `EquipmentManagementViewModel` | `T03-20261121-132600.md`, `T07-snapshot.txt` (EQ-008) |
| UC-16 Edit equipment | BR-05 to 07, 10, 14, 16, 18, 19 | API-01, 07, 09 | T03, T07 | `EquipmentController` | `FindEquipmentService`, `GetEquipmentService`, `UpdateEquipmentService` | `EquipmentManagementViewModel` | `T03-20261121-132600.md` |

## Endpoint index (API-01 to API-16)

| API | Method and route | Controller | Tests |
|---|---|---|---|
| API-01 | GET `/api/v1/equipment` | `EquipmentController` | T01, T07 |
| API-02 | GET `/api/v1/students` | `StudentsController` | T01, T02, T07 |
| API-03 | GET `/api/v1/students/{id}` | `StudentsController` | T01, T02, T07 |
| API-04 | POST `/api/v1/students` | `StudentsController` | T02, T07 |
| API-05 | PUT `/api/v1/students/{id}` | `StudentsController` | T02, T07 |
| API-06 | PATCH `/api/v1/students/{id}/eligibility` | `StudentsController` | T02, T07 |
| API-07 | GET `/api/v1/equipment/{id}` | `EquipmentController` | T01, T03, T07 |
| API-08 | POST `/api/v1/equipment` | `EquipmentController` | T03, T07 |
| API-09 | PUT `/api/v1/equipment/{id}` | `EquipmentController` | T03, T07 |
| API-10 | POST `/api/v1/borrowings` | `BorrowingsController` | T04, T05, T07 |
| API-11 | POST `/api/v1/checkout-requests` | `CheckoutRequestsController` | T05, T07 |
| API-12 | POST `/api/v1/borrowings/{id}/return` | `BorrowingsController` | T04, T07 |
| API-13 | GET `/api/v1/borrowings/{id}` | `BorrowingsController` | T01, T04, T07 |
| API-14 | GET `/api/v1/borrowings` | `BorrowingsController` | T01, T06, T07 |
| API-15 | GET `/api/v1/students/{id}/borrowings` | `StudentsController` | T01, T06, T07 |
| API-16 | GET `/api/v1/equipment/{id}/borrowings` | `EquipmentController` | T01, T06, T07 |

BR-18 (unreachable API versus a reachable server error, and unknown commit outcome) and BR-19 (relationships stay correct across histories) are covered by T08, T06 and the restart checks; see `docs/acceptance/test-results.md`.

Caveat on T07: the "Desktop view model" column is the code path the desktop uses for each use case. T07 exercised the register, edit, issue, return and history screens by hand and the independent-change refresh; it did not separately capture a still image of every screen. `docs/acceptance/test-results.md` states exactly which screenshots exist.

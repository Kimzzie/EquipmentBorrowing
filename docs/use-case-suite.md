# Use Case Suite (Lab 4): fourteen required cases

Adapted from the supplied *Lab Activity 4 Use Case Suite*. Every required field, flow, rule and acceptance outcome is preserved. Each table adds one **Implementation** row showing how the case is realised in this solution (path: View -> ViewModel -> Application Service -> Repository -> EF Core -> PostgreSQL). Primary actor: Laboratory Staff / Equipment Custodian. Supporting system: PostgreSQL for all cases. "None" under Includes / Extends means no UML relationship is required. All cases follow the business rules BR-01 to BR-19. Deferred cases (UC-10, 11, 17-27) and future-scope cases (UC-28-33) are only classified in `use-case-catalog.md`.

## UC-01: View Equipment Catalog

| Field | Specification |
|---|---|
| Use Case ID | UC-01 |
| Use Case Name | View Equipment Catalog |
| Scope | Equipment catalog |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff opens the equipment catalog. |
| Preconditions | Application is running. |
| Success Guarantee | Current equipment identifiers, names, descriptions and availability are displayed; no records change. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18. |
| Main Success Scenario | 1. Staff opens the catalog.<br>2. System retrieves equipment from PostgreSQL.<br>3. System displays each item and availability. |
| Alternative Flows | 2a. No equipment: system displays an empty state. |
| Exception Flows | 2e. Database unavailable: system displays an error and retry option. |
| Business Rules | BR-05-07, BR-10, BR-18-19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities. |
| Includes / Extends | None required. |
| Acceptance Criteria | Catalog matches database records, including availability; empty and database-error states are distinguishable. See A01. |
| Open Issues | None. |
| Implementation | Equipment screen -> EquipmentViewModel -> FindEquipmentService -> EfEquipmentRepository |

## UC-02: Find Available Equipment

| Field | Specification |
|---|---|
| Use Case ID | UC-02 |
| Use Case Name | Find Available Equipment |
| Scope | Equipment catalog |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff requests available equipment. |
| Preconditions | Application is running. |
| Success Guarantee | Only currently available items are shown; no records change. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18. |
| Main Success Scenario | 1. Staff chooses the available-items view/filter.<br>2. System retrieves current equipment state.<br>3. System displays only available items. |
| Alternative Flows | 3a. No items available: system shows an empty result. |
| Exception Flows | 2e. Database failure: system shows an error and permits retry. |
| Business Rules | BR-10, BR-18-19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities. |
| Includes / Extends | None required. |
| Acceptance Criteria | Available items appear, unavailable items do not, and no-match/error states differ. See A02. |
| Open Issues | None. |
| Implementation | Equipment screen ('Show available only') -> EquipmentViewModel -> FindEquipmentService -> EfEquipmentRepository |

## UC-03: Borrow Equipment

| Field | Specification |
|---|---|
| Use Case ID | UC-03 |
| Use Case Name | Borrow Equipment |
| Scope | Borrowing |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff submits a single-item borrowing. |
| Preconditions | Application is running; staff can select a student and item. |
| Success Guarantee | One active borrowing and the corresponding unavailable state are persisted together. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18. |
| Main Success Scenario | 1. Staff selects a student, item and expected return date.<br>2. System revalidates student, limit, date and item.<br>3. System saves the active borrowing and availability change.<br>4. System confirms and refreshes. |
| Alternative Flows | 2a. Missing, ineligible, over-limit, unavailable or invalid-date request: system rejects with a reason and no change. |
| Exception Flows | 2e. Stale record or conflict: system rejects and refreshes.<br>3e. Database failure: system reports failure or unknown outcome under BR-18. |
| Business Rules | BR-08-11, BR-14-16, BR-18-19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities. |
| Includes / Extends | None required. |
| Acceptance Criteria | Valid single issue persists; each invalid condition is rejected without changes; outcome survives restart. See A03. |
| Open Issues | None. |
| Implementation | Equipment screen -> EquipmentViewModel -> BorrowEquipmentService -> CheckoutProcessor -> EfStudent/Equipment/BorrowingRepository + EfUnitOfWork (Serializable transaction) |

## UC-04: View Active Borrowings

| Field | Specification |
|---|---|
| Use Case ID | UC-04 |
| Use Case Name | View Active Borrowings |
| Scope | Borrowing |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff opens active borrowings. |
| Preconditions | Application is running. |
| Success Guarantee | Only active records with related students and equipment are displayed; nothing changes. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18. |
| Main Success Scenario | 1. Staff opens active borrowings.<br>2. System retrieves active records and related student and equipment.<br>3. System displays identities and relevant dates. |
| Alternative Flows | 3a. No active records: system displays an empty state. |
| Exception Flows | 2e. Database failure: system reports an error and allows retry. |
| Business Rules | BR-14, BR-18-19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities. |
| Includes / Extends | None required. |
| Acceptance Criteria | Active records and related identities are correct; returned records are excluded; empty/error states differ. See A04. |
| Open Issues | None. |
| Implementation | Active Borrowings screen -> BorrowingsViewModel -> ViewActiveBorrowingsService -> EfBorrowingRepository |

## UC-05: Return Equipment

| Field | Specification |
|---|---|
| Use Case ID | UC-05 |
| Use Case Name | Return Equipment |
| Scope | Borrowing |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff submits return of a selected active borrowing. |
| Preconditions | An active borrowing can be selected. |
| Success Guarantee | The same borrowing is returned with a return date and the item becomes available, with history preserved. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18. |
| Main Success Scenario | 1. Staff selects an active borrowing and confirms return.<br>2. System verifies its current status.<br>3. System records return and availability together.<br>4. System confirms and refreshes. |
| Alternative Flows | 2a. Already returned: system rejects the repeat without changing data. |
| Exception Flows | 2e. Missing or stale record: system reports the issue and refreshes.<br>3e. Database failure: system reports failure or unknown outcome under BR-18. |
| Business Rules | BR-10, BR-13-16, BR-18-19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities. |
| Includes / Extends | None required. |
| Acceptance Criteria | Return persists across restart, equipment is available, history remains, and repeat return is rejected. See A05. |
| Open Issues | None. |
| Implementation | Active Borrowings screen -> BorrowingsViewModel -> ReturnEquipmentService -> EfBorrowingRepository + EfEquipmentRepository + EfUnitOfWork |

## UC-06: Check Out Multiple Equipment Items

| Field | Specification |
|---|---|
| Use Case ID | UC-06 |
| Use Case Name | Check Out Multiple Equipment Items |
| Scope | Borrowing |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff submits a multi-item checkout. |
| Preconditions | Application is running; student and equipment can be selected. |
| Success Guarantee | One active borrowing per selected item and all corresponding availability changes are committed together. |
| Failure Guarantee | A known failed or rejected transaction leaves no changes; if commit acknowledgement is lost, the outcome is unknown until verified, with no partial commit. |
| Main Success Scenario | 1. Staff selects a student, at least two items and an expected return date.<br>2. System revalidates current references, eligibility, count, item availability and date.<br>3. System commits all records and availability changes as one transaction.<br>4. System confirms and refreshes. |
| Alternative Flows | 2a. Duplicate/fewer than two items, invalid date, ineligible student, limit violation or unavailable item: system rejects with a reason; staff may correct. |
| Exception Flows | 2e. Stale or conflicting state: system rejects and refreshes.<br>3e. Known write failure: system rolls back and reports failure.<br>3f. Commit outcome unknown: system follows BR-18. |
| Business Rules | BR-08-12, BR-14-16, BR-18-19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities. |
| Includes / Extends | None required. |
| Acceptance Criteria | Two valid items produce two persisted active records; each invalid request produces none; controlled in-transaction failure leaves no partial changes. See A06. |
| Open Issues | None. |
| Implementation | Equipment screen (checkout list) -> EquipmentViewModel -> CheckOutMultipleEquipmentService -> CheckoutProcessor -> EfStudent/Equipment/BorrowingRepository + EfUnitOfWork (Serializable transaction) |

## UC-07: View Student Borrowing History

| Field | Specification |
|---|---|
| Use Case ID | UC-07 |
| Use Case Name | View Student Borrowing History |
| Scope | Student history |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff selects a student and opens history. |
| Preconditions | Application is running; student selection is available. |
| Success Guarantee | Only the selected student's active and returned records and related equipment are shown; no data changes. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18. |
| Main Success Scenario | 1. Staff selects a student.<br>2. System asynchronously retrieves related borrowings and equipment.<br>3. System displays student identity, item, checkout, expected return, status and actual return when present. |
| Alternative Flows | 3a. No history: system displays an empty state.<br>1a. Staff selects another student: system replaces results with that student's data. |
| Exception Flows | 2e. Missing/stale student or database failure: system explains the problem and allows reselection or retry. |
| Business Rules | BR-14, BR-18-19 |
| Special Requirements | PostgreSQL-backed asynchronous loading, responsive window, loading and error states. |
| Includes / Extends | None required. |
| Acceptance Criteria | Two students' distinguishable records do not mix; active, returned and empty results are accurate; loading is responsive. See A07. |
| Open Issues | None. |
| Implementation | History screen (By student) -> HistoryViewModel + HistoryPanelViewModel -> ViewStudentHistoryService -> EfBorrowingQueryRepository |

## UC-08: View Equipment Borrowing History

| Field | Specification |
|---|---|
| Use Case ID | UC-08 |
| Use Case Name | View Equipment Borrowing History |
| Scope | Equipment history |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff selects equipment and opens history. |
| Preconditions | Application is running; equipment selection is available. |
| Success Guarantee | Only the selected item's active and returned records and related students are shown; no data changes. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18. |
| Main Success Scenario | 1. Staff selects an item.<br>2. System asynchronously retrieves its borrowings and students.<br>3. System displays equipment identity, student, checkout, expected return, status and actual return when present. |
| Alternative Flows | 3a. No history: system displays an empty state.<br>1a. Staff selects another item: system replaces the details. |
| Exception Flows | 2e. Missing/stale item or database failure: system explains the problem and allows reselection or retry. |
| Business Rules | BR-14, BR-18-19 |
| Special Requirements | PostgreSQL-backed asynchronous loading, responsive window, loading and error states. |
| Includes / Extends | None required. |
| Acceptance Criteria | Two items' distinguishable records do not mix; active, returned and empty results are accurate; loading is responsive. See A08. |
| Open Issues | None. |
| Implementation | History screen (By equipment) -> HistoryViewModel + HistoryPanelViewModel -> ViewEquipmentHistoryService -> EfBorrowingQueryRepository |

## UC-09: Search, Filter, and Sort Borrowings

| Field | Specification |
|---|---|
| Use Case ID | UC-09 |
| Use Case Name | Search, Filter, and Sort Borrowings |
| Scope | Borrowing queries |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff enters criteria or opens the borrowing list. |
| Preconditions | Application is running. |
| Success Guarantee | Only matching records are shown in the requested order; no data changes. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18. |
| Main Success Scenario | 1. Staff opens borrowings and chooses search text, status filter and/or sort.<br>2. System asynchronously loads matching related records.<br>3. System displays student, equipment, dates and status in the selected order.<br>4. Staff may clear criteria. |
| Alternative Flows | 1a. Criteria may be used separately or together.<br>3a. No matches: system shows an empty result.<br>4a. Clear restores the full list. |
| Exception Flows | 2e. Database failure: system reports an error and offers retry. |
| Business Rules | BR-17-19 |
| Special Requirements | PostgreSQL-backed asynchronous loading, responsive window, loading and error states. |
| Includes / Extends | None required. |
| Acceptance Criteria | Search each specified field, each status, both sort fields/directions, combined criteria and reset against known data. See A09. |
| Open Issues | None. |
| Implementation | Search Borrowings screen -> BorrowingSearchViewModel -> SearchBorrowingsService -> EfBorrowingQueryRepository |

## UC-12: Register Student

| Field | Specification |
|---|---|
| Use Case ID | UC-12 |
| Use Case Name | Register Student |
| Scope | Student management |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff submits a new student. |
| Preconditions | Application is running; registration form is accessible. |
| Success Guarantee | One uniquely identified student with the supplied details and default eligibility is persisted. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18. |
| Main Success Scenario | 1. Staff enters institutional number, full name and optional contact.<br>2. System validates required fields and uniqueness.<br>3. System saves the student.<br>4. System confirms and displays the saved record. |
| Alternative Flows | 2a. Missing/blank required data or duplicate normalized number: system rejects and identifies the problem.<br>1a. Optional contact omitted: system stores it as absent. |
| Exception Flows | 3e. Concurrent duplicate or database failure: system reports failure or unknown outcome under BR-18; no duplicate is created. |
| Business Rules | BR-01-02, BR-16, BR-18-19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities. |
| Includes / Extends | None required. |
| Acceptance Criteria | Valid student can be retrieved after restart; empty name/number and duplicate number are rejected. See A12. |
| Open Issues | None. |
| Implementation | Students screen -> StudentsViewModel -> RegisterStudentService -> EfStudentRepository + EfUnitOfWork |

## UC-13: Update Student Information

| Field | Specification |
|---|---|
| Use Case ID | UC-13 |
| Use Case Name | Update Student Information |
| Scope | Student management |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff submits changes to a selected student. |
| Preconditions | A student exists and can be located and selected. |
| Success Guarantee | Allowed changes persist for the same student; borrowing relationships and history remain intact. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18. |
| Main Success Scenario | 1. Staff finds and selects a student.<br>2. Staff edits permitted fields and submits.<br>3. System validates and saves the changes.<br>4. System confirms and displays the updated record. |
| Alternative Flows | 2a. Staff leaves optional contact blank: system clears it.<br>3a. Blank required field or duplicate number: system rejects without changes. |
| Exception Flows | 3e. Student no longer exists, conflicting update or database failure: system reports the issue and refreshes/retries under BR-18. |
| Business Rules | BR-01-03, BR-14, BR-16, BR-18-19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities. |
| Includes / Extends | None required. |
| Acceptance Criteria | Locate and update a student, restart, and verify changed details and unchanged linked borrowings; reject invalid and duplicate edits. See A13. |
| Open Issues | None. |
| Implementation | Students screen -> StudentsViewModel -> UpdateStudentService -> EfStudentRepository + EfUnitOfWork |

## UC-14: Change Student Borrowing Eligibility

| Field | Specification |
|---|---|
| Use Case ID | UC-14 |
| Use Case Name | Change Student Borrowing Eligibility |
| Scope | Student management |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff requests a change to a student's eligibility. |
| Preconditions | A student exists and can be selected. |
| Success Guarantee | Requested eligibility is persisted; existing active borrowings and history are unchanged. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18. |
| Main Success Scenario | 1. Staff finds and selects a student.<br>2. Staff chooses permit or restrict and submits.<br>3. System saves and confirms the current eligibility. |
| Alternative Flows | 2a. Requested state is already set: system informs staff and makes no unintended changes.<br>3a. Restricted student with active loans may still return them. |
| Exception Flows | 3e. Student missing/stale or database failure: system reports failure or unknown outcome under BR-18. |
| Business Rules | BR-04, BR-08, BR-13-14, BR-18-19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities. |
| Includes / Extends | None required. |
| Acceptance Criteria | Restriction persists and blocks a new issue without changing active loans; return remains possible; permitting allows an otherwise valid request. See A14. |
| Open Issues | None. |
| Implementation | Students screen -> StudentsViewModel -> ChangeStudentEligibilityService -> EfStudentRepository + EfUnitOfWork |

## UC-15: Register Equipment

| Field | Specification |
|---|---|
| Use Case ID | UC-15 |
| Use Case Name | Register Equipment |
| Scope | Equipment management |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff submits a new physical item. |
| Preconditions | Application is running; registration form is accessible. |
| Success Guarantee | One uniquely identified item is persisted as available with no active borrowing. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18. |
| Main Success Scenario | 1. Staff enters an equipment identifier, name and optional description.<br>2. System validates required fields and uniqueness.<br>3. System saves the item with initial availability.<br>4. System confirms and displays it. |
| Alternative Flows | 2a. Required field blank or normalized identifier duplicate: system rejects and identifies the issue.<br>1a. Optional description omitted: system stores it as absent. |
| Exception Flows | 3e. Concurrent duplicate or database failure: system reports failure or unknown outcome under BR-18; no duplicate is created. |
| Business Rules | BR-05-06, BR-10, BR-16, BR-18-19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities. |
| Includes / Extends | None required. |
| Acceptance Criteria | Valid item appears as available after restart; missing required fields and duplicate identifier are rejected. See A15. |
| Open Issues | None. |
| Implementation | Manage Equipment screen -> EquipmentManagementViewModel -> RegisterEquipmentService -> EfEquipmentRepository + EfUnitOfWork |

## UC-16: Update Equipment Information

| Field | Specification |
|---|---|
| Use Case ID | UC-16 |
| Use Case Name | Update Equipment Information |
| Scope | Equipment management |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff submits changes to selected equipment. |
| Preconditions | An item exists and can be located and selected. |
| Success Guarantee | Allowed descriptive changes persist for the same item; availability and borrowing history remain intact. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18. |
| Main Success Scenario | 1. Staff finds and selects an item.<br>2. Staff edits permitted identifier, name or description and submits.<br>3. System validates and saves changes.<br>4. System confirms and displays updated details. |
| Alternative Flows | 2a. Staff clears optional description: system stores it as absent.<br>3a. Blank required field or duplicate identifier: system rejects without changes.<br>2b. Availability and borrowing state cannot be edited as descriptive fields. |
| Exception Flows | 3e. Item missing/stale, conflicting update or database failure: system reports the issue and refreshes/retries under BR-18. |
| Business Rules | BR-05-07, BR-10, BR-14, BR-16, BR-18-19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities. |
| Includes / Extends | None required. |
| Acceptance Criteria | Edit and restart preserves new details and linked history/availability; duplicate and blank required edits are rejected. See A16. |
| Open Issues | None. |
| Implementation | Manage Equipment screen -> EquipmentManagementViewModel -> UpdateEquipmentService -> EfEquipmentRepository + EfUnitOfWork |


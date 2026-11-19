# Traceability Matrix: use cases to implementation and acceptance evidence

Path for every case: View -> ViewModel -> Application Service -> Repository interface -> EF Core repository -> PostgreSQL.

| UC | Screen -> ViewModel | Application service | Repository / infrastructure | Rules | Acceptance | Rubric |
|---|---|---|---|---|---|---|
| UC-01 | Equipment -> EquipmentViewModel | FindEquipmentService | EfEquipmentRepository | BR-05-07, 10, 18-19 | A01 | R1 |
| UC-02 | Equipment ("Show available only") -> EquipmentViewModel | FindEquipmentService | EfEquipmentRepository | BR-10, 18-19 | A02 | R1 |
| UC-03 | Equipment -> EquipmentViewModel | BorrowEquipmentService -> CheckoutProcessor | EfStudent/Equipment/BorrowingRepository, EfUnitOfWork (Serializable transaction) | BR-08-11, 14-16, 18-19 | A03 | R1, R2 |
| UC-04 | Active Borrowings -> BorrowingsViewModel | ViewActiveBorrowingsService | EfBorrowingRepository | BR-14, 18-19 | A04 | R1 |
| UC-05 | Active Borrowings -> BorrowingsViewModel | ReturnEquipmentService | EfBorrowingRepository, EfEquipmentRepository, EfUnitOfWork | BR-10, 13-16, 18-19 | A05 | R1, R2 |
| UC-06 | Equipment (checkout list) -> EquipmentViewModel | CheckOutMultipleEquipmentService -> CheckoutProcessor | as UC-03 | BR-08-12, 14-16, 18-19 | A06 | R3 |
| UC-07 | History, By student -> HistoryViewModel + HistoryPanelViewModel | ViewStudentHistoryService | EfBorrowingQueryRepository | BR-14, 18-19 | A07 | R6, R7 |
| UC-08 | History, By equipment -> HistoryViewModel + HistoryPanelViewModel | ViewEquipmentHistoryService | EfBorrowingQueryRepository | BR-14, 18-19 | A08 | R6, R7 |
| UC-09 | Search Borrowings -> BorrowingSearchViewModel | SearchBorrowingsService | EfBorrowingQueryRepository | BR-17-19 | A09 | R6, R7 |
| UC-12 | Students -> StudentsViewModel | RegisterStudentService | EfStudentRepository, EfUnitOfWork | BR-01-02, 16, 18-19 | A12 | R4, R2 |
| UC-13 | Students -> StudentsViewModel | UpdateStudentService | EfStudentRepository, EfUnitOfWork | BR-01-03, 14, 16, 18-19 | A13 | R4 |
| UC-14 | Students -> StudentsViewModel | ChangeStudentEligibilityService | EfStudentRepository, EfUnitOfWork | BR-04, 08, 13-14, 18-19 | A14 | R4 |
| UC-15 | Manage Equipment -> EquipmentManagementViewModel | RegisterEquipmentService | EfEquipmentRepository, EfUnitOfWork | BR-05-06, 10, 16, 18-19 | A15 | R5, R2 |
| UC-16 | Manage Equipment -> EquipmentManagementViewModel | UpdateEquipmentService | EfEquipmentRepository, EfUnitOfWork | BR-05-07, 10, 14, 16, 18-19 | A16 | R5 |

Cross-cutting: A17 (migrations and constraints), A18 (failure feedback, context lifetimes, transaction boundary) and A19 (documentation agrees with implementation) are evidenced in `evidence.md`.

Database constraints behind the rules (migration `InitialPostgreSql`): `UX_Students_NormalizedStudentNumber` (BR-01), `UX_Equipment_NormalizedEquipmentCode` (BR-05), `UX_Borrowings_OneActivePerEquipment` (BR-10), `CK_Borrowings_ExpectedAfterBorrowed` (BR-11), `CK_Borrowings_StatusDates` (BR-13), foreign keys with Restrict (BR-14), not-blank checks (BR-01, 02, 05, 06).

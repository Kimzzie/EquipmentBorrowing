# Laboratory Activity 4 — Acceptance and Evidence (Appendix C)

**Students:** <Full Name 1> (<Student ID>) and <Full Name 2> (<Student ID>)
**Repository:** https://github.com/Kimzzie/EquipmentBorrowing
**Database used for all evidence:** disposable PostgreSQL database `equipment_borrowing_test` (never the working database).
**Note:** Borrowing dates in the test database were spread by SQL so that date sorting could be demonstrated (A09).

## How to use this file
- Every screenshot is an image link of the form `![name](evidence/name.png)`. Save each screenshot into `docs/evidence/` using **exactly** the file name shown, and it will appear here without editing this file.
- Lines starting with **Shows:** say what the screenshot must contain. If a screenshot does not show that, retake it.
- **Observed** sentences are drafts. Edit anything that differs from what really happened. Do not leave a claim you did not see.
- A screenshot alone does not prove persistence or atomicity, so each scenario also lists a database readback taken in a **fresh** pgAdmin Query Tab.
- Items marked *(optional)* may be skipped; if skipped, delete the line so the file does not claim them.

## Summary

| ID | Use case | Criterion | Status |
|---|---|---|---|
| A01 | UC-01 View Equipment Catalog | R1 | ☐ |
| A02 | UC-02 Find Available Equipment | R1 | ☐ |
| A03 | UC-03 Borrow Equipment | R1, R2 | ☐ |
| A04 | UC-04 View Active Borrowings | R1 | ☐ |
| A05 | UC-05 Return Equipment | R1, R2 | ☐ |
| A06 | UC-06 Check Out Multiple Items | R3 | ☐ |
| A07 | UC-07 Student Borrowing History | R6, R7 | ☐ |
| A08 | UC-08 Equipment Borrowing History | R6, R7 | ☐ |
| A09 | UC-09 Search, Filter, Sort | R6, R7 | ☐ |
| A12 | UC-12 Register Student | R4, R2 | ☐ |
| A13 | UC-13 Update Student | R4 | ☐ |
| A14 | UC-14 Change Eligibility | R4 | ☐ |
| A15 | UC-15 Register Equipment | R5, R2 | ☐ |
| A16 | UC-16 Update Equipment | R5 | ☐ |
| A17 | Migrations and constraints | R2 | ☐ |
| A18 | Failure, recovery, architecture | R3, R7, R8 | ☐ |
| A19 | Documents and README agree | R8 | ☐ |

A10 and A11 are not in this lab (UC-10 and UC-11 are deferred to Lab 5).

---

## A01 — UC-01 View Equipment Catalog (R1)
**Rules:** BR-05–07, 10, 18–19
**Setup:** Test database with several items, some available and some borrowed.
**Action:** Open the catalog; repeat with the database stopped.
**Expected:** No data changes. The list matches the database. Empty and error states are different messages.
**Observed:** The catalog lists every item with code, name and availability and matches the database. A stopped database showed a "Cannot reach the PostgreSQL server" message with Retry, not a crash.

![A01-catalog](evidence/A01-catalog.png)
**Shows:** the equipment catalog with codes, names, availability.

![A01-db-compare](evidence/A01-db-compare.png)
**Shows:** pgAdmin result of `SELECT "Id","EquipmentCode","Name","IsAvailable" FROM "Equipment" ORDER BY "Id";` matching the catalog.

![A01-empty](evidence/A01-empty.png)
**Shows:** the empty-catalog message. *(optional if not demonstrated)*

![A01-error](evidence/A01-error.png)
**Shows:** the connection-failure message with the PostgreSQL service stopped.

---

## A02 — UC-02 Find Available Equipment (R1)
**Rules:** BR-10, 18–19
**Setup:** Some items available, some on loan.
**Action:** Open the available list; make every item unavailable for the "none" case; restore; stop the database for the error case.
**Expected:** No changes. Unavailable items are excluded. No-match is an empty state, not an error.
**Observed:** Only available items were listed and the list matched the database. With none available, an empty message appeared. After restoring, the items returned.

![A02-all](evidence/A02-all.png)
**Shows:** available-equipment screen unfiltered.

![A02-filtered](evidence/A02-filtered.png)
**Shows:** a search narrowing the list.

![A02-readback](evidence/A02-readback.png)
**Shows:** SQL `SELECT "EquipmentCode","Name" FROM "Equipment" WHERE "IsAvailable" ORDER BY 1;` matching the screen.

![A02-none](evidence/A02-none.png)
**Shows:** the "no available equipment" empty state.

![A02-restore](evidence/A02-restore.png)
**Shows:** the list after items were returned or restored.

![A02-error](evidence/A02-error.png)
**Shows:** failed-load message with the database stopped.

---

## A03 — UC-03 Borrow Equipment (R1, R2)
**Rules:** BR-08–11, 14–16, 18–19
**Setup:** An eligible student, an available item, a restricted student, an unavailable item, a student at the 3-item limit.
**Action:** Borrow one item successfully; separately try restricted, unavailable, over-limit and invalid-date requests; restart the app.
**Expected:** Success creates one Active borrowing and makes the item unavailable. Each rejection leaves the data unchanged. State survives restart.
**Observed:** The borrow succeeded and the item became unavailable. Restricted, unavailable, over-limit and invalid-date requests each showed a clear message and added no rows. The record was still there after restart.

![A03-before](evidence/A03-before.png)
**Shows:** database counts/listing before the borrow.

![A03-success](evidence/A03-success.png)
**Shows:** the success message in the app.

![A03-after](evidence/A03-after.png)
**Shows:** fresh-tab SQL: one new Active row and the item unavailable.

![A03-date](evidence/A03-date.png)
**Shows:** rejection of an invalid expected-return date (not later than checkout).

![A03-restricted](evidence/A03-restricted.png)
**Shows:** rejection for a student who may not borrow.

![A03-unavailable](evidence/A03-unavailable.png)
**Shows:** rejection of an unavailable item.

![A03-limit](evidence/A03-limit.png)
**Shows:** rejection when the student already has 3 active items.

![A03-final](evidence/A03-final.png)
**Shows:** fresh-tab SQL after all rejections: counts unchanged since the success.

![A03-restart](evidence/A03-restart.png)
**Shows:** the borrowing still present after closing and reopening the app.

*Missing or stale reference (BR-08):* the picker only offers existing records, so this case is covered by source inspection (`CheckoutProcessor` revalidates student and item inside the transaction), not a screenshot.

---

## A04 — UC-04 View Active Borrowings (R1)
**Rules:** BR-14, 18–19
**Setup:** Mix of active and returned borrowings.
**Action:** Open Active Borrowings; test the empty and connection-error cases.
**Expected:** No changes. Returned records are excluded. Results match the database.
**Observed:** Only Active borrowings were listed, with student and equipment names, and they matched the database. 

![A04-list](evidence/A04-list.png)
**Shows:** the active list with student and equipment.

![A04-readback](evidence/A04-readback.png)
**Shows:** SQL listing Active rows (join Students/Equipment, `WHERE b."Status"='Active'`) matching the screen.

![A04-error](evidence/A04-error.png)
**Shows:** the connection-error message.

---

## A05 — UC-05 Return Equipment (R1, R2)
**Rules:** BR-10, 13–16, 18–19
**Setup:** An active borrowing.
**Action:** Return it; try returning it again; try a stale selection (returned by another user via SQL); restart.
**Expected:** The same record becomes Returned with a date, the item becomes available and history is kept. A repeat return changes nothing.
**Observed:** The return succeeded: Status Returned, DateReturned set, item available. The repeat/stale return was rejected with a message and no change. The result survived restart.

![A05-before](evidence/A05-before.png)
**Shows:** the active borrowing before return (app + SQL).

![A05-success](evidence/A05-success.png)
**Shows:** the success message after return.

![A05-readback](evidence/A05-readback.png)
**Shows:** fresh-tab SQL: the same `Id` is Returned with `DateReturned`, item `IsAvailable`=true.

![A05-repeat](evidence/A05-repeat.png)
**Shows:** the rejection of a repeat return.

![A05-restart](evidence/A05-restart.png)
**Shows:** the returned state after restarting the app.

---

## A06 — UC-06 Check Out Multiple Items (R3)
**Rules:** BR-08–12, 14–16, 18–19
**Setup:** An eligible student, at least two available items, plus a restricted student, an unavailable item and a student near the limit.
**Action:** Issue two distinct items; reject duplicate, fewer than two, unavailable, invalid-date, restricted and over-limit; force failures inside the transaction.
**Expected:** Success persists both Active rows and both availability changes. Every rejection or known failure leaves no changes. Fresh before/after reads are compared.
**Observed:** Two items were issued together and both rows persisted. Each rejection showed a message and left the counts unchanged. With `BeforeCommit` the transaction rolled back and the fresh read showed no new rows. With `AfterCommit` the commit completed, the app reported an unknown outcome, and the fresh read showed the rows were saved.

![A06-before](evidence/A06-before.png)
**Shows:** fresh-tab counts/listing before the checkout.

![A06-success](evidence/A06-success.png)
**Shows:** the success message and the two items issued.

![A06-readback](evidence/A06-readback.png)
**Shows:** fresh-tab SQL: two new Active rows, both items unavailable.

![A06-duplicate](evidence/A06-duplicate.png)
**Shows:** the duplicate-item rejection.

![A06-one](evidence/A06-one.png)
**Shows:** the "at least two items" rejection.

![A06-unavailable](evidence/A06-unavailable.png)
**Shows:** the rejection when one selected item is unavailable.

![A06-date](evidence/A06-date.png)
**Shows:** the invalid-date rejection.

![A06-restricted](evidence/A06-restricted.png)
**Shows:** the rejection for a restricted student.

![A06-limit](evidence/A06-limit.png)
**Shows:** the over-limit rejection (existing active + requested > 3).

![A06-exact-limit](evidence/A06-exact-limit.png)
**Shows:** a checkout that ends exactly at 3 active items succeeding. *(optional, delete if not run)*


### Safe failure inside the transaction (BR-15, BR-18)
**Mechanism:** the fault seam `FaultInjection` (Infrastructure/Data) is called from `EfUnitOfWorkTransaction.CommitAsync`. It reads the environment variable `EQUIPMENT_BORROWING_FAULT` and only acts when the database name ends with `_test`. Writes have already been sent to PostgreSQL when it fires.

![A06-beforecommit-app](evidence/A06-beforecommit-app.png)
**Shows:** the app's error message after `BeforeCommit`.

![A06-beforecommit-db](evidence/A06-beforecommit-db.png)
**Shows:** fresh-tab SQL before vs after: no new borrowings, availability unchanged (rolled back).

![A06-aftercommit-app](evidence/A06-aftercommit-app.png)
**Shows:** the "outcome unknown, please verify" message after `AfterCommit`, with no automatic retry.

![A06-aftercommit-db](evidence/A06-aftercommit-db.png)
**Shows:** fresh-tab SQL: the rows exist (the commit did happen), proving the app did not claim a rollback.

---

## A07 — UC-07 View Student Borrowing History (R6, R7)
**Rules:** BR-14, 18–19
**Setup:** Two students with distinguishable active and returned histories, and one student with none.
**Action:** Open each student's history; try a missing/stale selection; stop the database.
**Expected:** No changes. Each history shows the right item, dates and status. Empty and error states are different.
**Observed:** Each student's history showed the correct items, dates and status. A student with no borrowings showed an empty message. A stopped database showed an error, not an empty list.

![A07-student-returned](evidence/A07-student-returned.png)
**Shows:** a student whose history includes Returned records (Kimitzu).

![A07-student-active](evidence/A07-student-active.png)
**Shows:** a student with Active borrowings (Bryl Lim).

![A07-student-none](evidence/A07-student-none.png)
**Shows:** a student with no borrowings (Test Student), with the empty message.

![A07-error](evidence/A07-error.png)
**Shows:** the error message with the database stopped (reuse the A01 error screenshot style).

**Asynchronous path (source inspection):** history is loaded by an `async` ViewModel command that shows a loading state and awaits the service; the service awaits the repository, which uses EF Core async queries. No SQL or `DbContext` appears in the View or ViewModel.

---

## A08 — UC-08 View Equipment Borrowing History (R6, R7)
**Rules:** BR-14, 18–19
**Setup:** Two items with distinguishable active/returned histories and one with none (for example EQ-007).
**Action:** Open each item's history; try a missing/stale selection; stop the database.
**Expected:** No changes. Correct student, dates and status per item; empty and error distinguished.
**Observed:** Each item's history showed the correct student, dates and status. An item with no history showed an empty message.

![A08-equipment-returned](evidence/A08-equipment-returned.png)
**Shows:** an item with Returned history (for example EQ-001).

![A08-equipment-active](evidence/A08-equipment-active.png)
**Shows:** an item that is currently Active (for example EQ-002).

![A08-equipment-none](evidence/A08-equipment-none.png)
**Shows:** an item with no history (EQ-007) and the empty message.

**Asynchronous path:** same as A07 (async command, loading state, async repository query).

---

## A09 — UC-09 Search, Filter, Sort Borrowings (R6, R7)
**Rules:** BR-17–19
**Setup:** A known mixture of active and returned records with spread dates.
**Action:** Search each field; use each status; sort by both date fields in both directions; combine criteria; reset; no-match; stop the database.
**Expected:** No changes. Membership and order match controlled records. A no-match is an empty state, not an error. The window stays responsive while loading.
**Observed:** Search matched student name, student number and equipment name. Status filters showed the right subsets. Sorting reversed the order as expected. No-match showed an empty state. A stopped database showed a friendly error.

![A09-name](evidence/A09-name.png)
**Shows:** search by student name (for example `elon`).

![A09-number](evidence/A09-number.png)
**Shows:** search by student number (for example `2023-0003`).

![A09-equipment](evidence/A09-equipment.png)
**Shows:** search by equipment name (for example `Keyboard`).

![A09-filter-active](evidence/A09-filter-active.png)
**Shows:** status filter = Active.

![A09-filter-returned](evidence/A09-filter-returned.png)
**Shows:** status filter = Returned.

![A09-sort-asc](evidence/A09-sort-asc.png)
**Shows:** sort by checkout date ascending.

![A09-sort-expected](evidence/A09-sort-expected.png)
**Shows:** sort by expected return date (either direction). *(needed: BR-17 requires both sort fields)*

![A09-combined](evidence/A09-combined.png)
**Shows:** a search plus a status filter at the same time. *(needed)*

![A09-reset](evidence/A09-reset.png)
**Shows:** criteria cleared, full list back. *(needed)*

![A09-nomatch](evidence/A09-nomatch.png)
**Shows:** search `pax` giving the explicit empty state.

**Asynchronous path:** search runs in an async command with a loading indicator; sorting and filtering are done in the query (`ORDER BY`, `WHERE`), with `Id` as tie-breaker.

---

## A12 — UC-12 Register Student (R4, R2)
**Rules:** BR-01–02, 16, 18–19
**Action:** Register `2026-0100 / Test Student`; repeat with spaces and different case; submit blank fields.
**Expected:** Exactly one normalized-unique student persists after restart; rejected requests add nothing.
**Observed:** The student was registered and listed. A duplicate number (with spaces or different case) and blank fields were rejected with clear messages. The student appeared once after restart.

![A12-success](evidence/A12-success.png)
**Shows:** the student registered and eligible.

![A12-duplicate](evidence/A12-duplicate.png)
**Shows:** duplicate-number rejection (with spaces or case change).

![A12-blank](evidence/A12-blank.png)
**Shows:** blank required-field rejection.

---

## A13 — UC-13 Update Student (R4)
**Rules:** BR-01–03, 14, 16, 18–19
**Action:** Edit a student who has history; clear the optional contact; submit blank fields; use a duplicate number.
**Expected:** Edits persist on the same identity; borrowing history is intact; rejected updates change nothing.
**Observed:** The edit saved and the student's history was unchanged. A duplicate number and blank required fields were rejected with no change.

![A13-edit-history](evidence/A13-edit-history.png)
**Shows:** the edited student and their history still present.

![A13-blank](evidence/A13-blank.png)
**Shows:** blank name or number rejected. *(needed)*

![A13-duplicate](evidence/A13-duplicate.png)
**Shows:** duplicate-number rejection.

---

## A14 — UC-14 Change Student Eligibility (R4)
**Rules:** BR-04, 08, 13–14, 18–19
**Action:** Restrict a student with active borrowings (Bossing Jensen); try a new issue; return an existing item; permit; issue again; repeat the same eligibility choice.
**Expected:** Eligibility persists; active records are not cancelled; return still works; repeating the state gives feedback with no change.
**Observed:** The restricted student could not receive a new item but could still return an item. After permitting, a valid issue worked. Repeating the current state showed a message and changed nothing.

![A14-restricted](evidence/A14-restricted.png)
**Shows:** the student marked restricted.

![A14-issue-rejected](evidence/A14-issue-rejected.png)
**Shows:** a new issue rejected for the restricted student.

![A14-return-works](evidence/A14-return-works.png)
**Shows:** an existing item returned while restricted.

![A14-permitted](evidence/A14-permitted.png)
**Shows:** the student permitted again.

![A14-issue-after-permit](evidence/A14-issue-after-permit.png)
**Shows:** a valid issue succeeding after permit. *(needed)*

![A14-repeat](evidence/A14-repeat.png)
**Shows:** repeated-state feedback.

---

## A15 — UC-15 Register Equipment (R5, R2)
**Rules:** BR-05–06, 10, 16, 18–19
**Action:** Register `EQ-007 / Test Tripod`; repeat with ` eq-007 `; submit blank fields.
**Expected:** One new available item with no active borrowing persists after restart; invalid requests add none.
**Observed:** `EQ-007` was registered as available. A duplicate code (with spaces or case change) and a blank name were rejected. It was present after restart.

![A15-success](evidence/A15-success.png)
**Shows:** the new item, available.

![A15-duplicate](evidence/A15-duplicate.png)
**Shows:** duplicate-code rejection.

![A15-blank](evidence/A15-blank.png)
**Shows:** blank-field rejection.

---

## A16 — UC-16 Update Equipment (R5)
**Rules:** BR-05–07, 10, 14, 16, 18–19
**Action:** Edit an item that has history; clear the optional description; submit blank/duplicate values; try to change availability as descriptive data.
**Expected:** Permitted changes persist on the same identity; history and availability stay intact.
**Observed:** The description edit saved and the item's history remained. A duplicate code and blank values were rejected. Availability could not be edited from the edit form.

![A16-edit-history](evidence/A16-edit-history.png)
**Shows:** the edited item and its history still present.

![A16-clear-description](evidence/A16-clear-description.png)
**Shows:** the description cleared and saved. *(needed)*

![A16-blank](evidence/A16-blank.png)
**Shows:** blank required-field rejection. *(needed)*

![A16-duplicate](evidence/A16-duplicate.png)
**Shows:** duplicate-code rejection.

---

## A17 — Migrations and constraints (R2)
**Rules:** BR-01, 05, 10, 14, 16
**Setup:** A clean database created with `CREATE DATABASE equipment_borrowing_test OWNER equipment_app;`.
**Action:** Apply migrations; inspect schema and history; attempt invalid writes directly in SQL.
**Expected:** PostgreSQL rejects invalid writes; counts do not change.
**Observed:** The migration `InitialPostgreSql` was applied to a clean database. The schema has primary keys, non-cascading foreign keys, date/status check constraints, unique indexes on the normalized student number and equipment code, and a partial unique index allowing one Active borrowing per item. An invalid FK (`23503`), a duplicate student number, a duplicate equipment code and a second active borrowing (`23505` each) were all rejected, and counts were unchanged.

![A17-migration](evidence/A17-migration.png)
**Shows:** `dotnet ef database update` output ending with the migration applied.

![A17-history](evidence/A17-history.png)
**Shows:** `SELECT * FROM "__EFMigrationsHistory";`.

![A17-schema](evidence/A17-schema.png)
**Shows:** the tables in pgAdmin (Students, Equipment, Borrowings).

![A17-counts-before](evidence/A17-counts-before.png)
**Shows:** row counts before the bad inserts.

![A17-constraints-test](evidence/A17-constraints-test.png)
**Shows:** the Messages tab with four rejections: `23503`, `23505`, `23505`, `23505`.

![A17-counts-after](evidence/A17-counts-after.png)
**Shows:** the same counts, unchanged.

![A17-constraints](evidence/A17-constraints.png)
**Shows:** the `pg_constraint` listing.

![A17-indexes](evidence/A17-indexes.png)
**Shows:** the `pg_indexes` listing including `UX_Borrowings_OneActivePerEquipment`.

---

## A18 — Failure, recovery and architecture (R3, R7, R8)
**Rules:** BR-15, 18–19
**Action:** Stop the PostgreSQL service before an operation; restore it and retry; use the controlled fault seam (see A06); explain layers and lifetimes from the code.
**Expected:** A clear failure message, no false success, no partial changes for known failures, and a working retry after recovery.
**Observed:** With the service stopped, each screen showed an understandable error with Retry and the app did not close. After restarting the service, Retry worked. Testing found a bug: the Npgsql provider wraps transient errors in `InvalidOperationException`, and `DatabaseGuard` did not unwrap it, so the app closed silently. `DatabaseGuard` now walks the inner-exception chain, and the error screens appear.

![A18-db-down](evidence/A18-db-down.png)
**Shows:** an error screen while the service is stopped.

![A18-recovered](evidence/A18-recovered.png)
**Shows:** the same screen working after the service was restarted and Retry pressed.

![A18-fault](evidence/A18-fault.png)
**Shows:** reference to A06 `BeforeCommit` and `AfterCommit` evidence (no new screenshot needed).

### Code walkthrough
- **Layers:** View → ViewModel → Application service → repository interface → EF Core repository → PostgreSQL. Views and ViewModels contain no SQL and no `DbContext`.
- **Lifetimes:** ViewModels are transient and hold only `IServiceScopeFactory`. Each operation calls `CreateAsyncScope()`, so it gets its own scoped services and a fresh `DbContext`. A failed operation therefore cannot leave a poisoned context for the next one.
- **Transaction boundary:** `CheckoutProcessor` (Application layer) opens the transaction through `IUnitOfWork`, revalidates student, items and active count inside it, saves, then commits. The partial unique index is the database-level backstop against two active borrowings for one item.
- **Unknown outcomes:** a failure after the commit is reported as unknown (`CommitOutcomeUnknownException`), with no automatic retry and no claim of rollback.
- **Bug found and fixed:** `DatabaseGuard` unwrap fix, committed separately.

---

## A19 — Documents and README agree (R8)
**Action:** Review the diagram, catalog, suite and traceability against the implementation and evidence; follow the README setup and migration steps.
**Expected:** Source, instructions and evidence agree.
**Observed:** *(fill in after the final review)*

![A19-diagram](evidence/A19-diagram.png)
**Shows:** the updated use-case diagram with the custodian as primary actor.

![A19-readme-run](evidence/A19-readme-run.png)
**Shows:** the README setup steps run on a clean clone, with the app opening. *(optional)*

![A19-git-log](evidence/A19-git-log.png)
**Shows:** `git log --oneline` with meaningful commits.

**Documents:** [use-case-catalog.md](use-case-catalog.md), [use-case-suite.md](use-case-suite.md), [traceability.md](traceability.md), [database-queries.sql](database-queries.sql), [../README.md](../README.md).
# Laboratory Activity 5: Test Results (consolidated record)

**Author:** Kimberly Bautista, 2023303116 (individual submission, no partner)
**Course / activity:** ITSD81, Lab 5 API Integration and Acceptance Testing
**Executed:** 2026-11-21 (Asia/Manila, UTC+08:00)

## Environment

| Item | Value |
|---|---|
| OS / shell | Windows, PowerShell 7 (Developer PowerShell in Visual Studio) |
| .NET SDK | 10.0.404 |
| API | `src\EquipmentBorrowing.Api`, `http://localhost:5080`, launch profile `http-test-clock` |
| Fixed application clock | 2026-11-10 (the API logs "TEST CLOCK ACTIVE"); the server decides borrow and return dates |
| Database | PostgreSQL, disposable database `equipment_borrowing_lab5_test`. The reset tool and the fault switches refuse any database whose name does not end in `_test`. |
| Data reset | `dotnet run --project tools\EquipmentBorrowing.TestData -- reset`. Every `Invoke-Acceptance.ps1` test also resets its own data at the start. |
| Independent database read | `dotnet run --project tools\EquipmentBorrowing.TestData -- snapshot` |
| Test runner | `docs\acceptance\Invoke-Acceptance.ps1 -Test <id>`; it writes evidence files to `docs\evidence\lab5\` |
| Secrets | The connection string is read from an environment variable and is never written to evidence files. |

## Summary

| Test | Result | Evidence |
|---|---|---|
| T01 | Pass (45 of 45 checks) | `T01-20261121-132320.md`, `screenshots/T01-console-run.png` |
| T02 | Pass (15 of 15) | `T02-20261121-132520.md` |
| T03 | Pass (13 of 13) | `T03-20261121-132600.md` |
| T04 | Pass (15 of 15) | `T04-20261121-132630.md` |
| T05 steps 1 to 3 | Pass (14 of 14; run twice, see note) | `T05-20261121-132721.md`, `T05-20261121-134234.md` |
| T05 step 4 (injected failure) | Pass (5 of 5) | `T05-fault-20261121-133737.md` |
| T06 | Pass (42 of 42) | `T06-20261121-134007.md` |
| After restart | Pass (4 of 4) | `AfterRestart-20261121-134430.md` |
| T07 | Pass, with two screenshot gaps (see T07) | `T07-dependencies.txt`, `T07-snapshot.txt`, screenshots |
| T08 | Pass for API and database behaviour; two desktop-message screenshots not in the repository (see T08) | `T08-console-captures.txt`, `T08-snapshot-*.txt`, screenshots |

All evidence files are in `docs\evidence\lab5\`. An HTTP status alone was never used as evidence of persistence or rollback: each write test compares a fresh PostgreSQL read before and after. The baseline (reset plus snapshot) is in `screenshots/T-baseline-reset-and-snapshot.png`.

Note on T05: it was executed twice. The first run is `T05-20261121-132721.md`. After T05-fault I ran it again (reset first) so that the data written by the multi-item checkout could be used for the restart check; the second run is `T05-20261121-134234.md`. Both passed 14 of 14.

## Test execution records

### T01: Independent API reads (API-01 to 03, 07, 13 to 16)
- **Environment:** API alone, desktop closed, seed data.
- **Expected:** 200 arrays and objects match the seed; `available=true` excludes E1, E4 and E5; an empty history is `[]`; unknown ids and a wrong route return 404; a wrong method returns 405; the OpenAPI document is retrievable and describes all 16 contracts; no database change.
- **Observed:** 45 of 45 checks passed, including OpenAPI documenting API-01 to API-16 with their published response codes, and "database state unchanged".
- **Pass / fail:** Pass.
- **Issue / diagnosis / correction / retest:** see "Issues found and corrected" below. Earlier attempts failed on build and dependency problems, not on the test expectations.
- **Evidence:** `T01-20261121-132320.md`, `screenshots/T01-console-run.png`

### T02: Student create, edit and eligibility (API-04 to 06)
- **Expected:** 201 with `Location`; 200 for updates; 400 for a blank name; 409 for a duplicate (`" st-100 "`) and for a new issue to a restricted student; a restricted student can still return; the same eligibility again returns 200.
- **Observed:** 15 of 15 passed. Restricted S1 received 409 on a new issue and 200 when returning E1; history stayed intact; after permitting, a valid issue returned 201.
- **Pass / fail:** Pass.
- **Evidence:** `T02-20261121-132520.md`

### T03: Equipment create, edit and protected state (API-07 to 09)
- **Expected:** 201 with `Location` and the new item available; 200 for updates; 400 for a blank name and for `isAvailable` in an update; 409 for a duplicate (`" eq-001 "`); E1 stays unavailable with its history.
- **Observed:** 13 of 13 passed.
- **Pass / fail:** Pass.
- **Evidence:** `T03-20261121-132600.md`

### T04: Single issue and return (API-10, 12, 13)
- **Expected:** 201 then 200; rejected attempts (ineligible S2, unavailable E1, missing ids, bad dates) return 409, 404 or 400 with no writes; the return returns 200 with the fixed date; a repeat return returns 409.
- **Observed:** 15 of 15 passed; "rejected attempts wrote nothing"; `returnedDate` is 2026-11-10; the repeat return gave 409. Persistence across an API restart is shown by the After restart run and by T08 step 3b.
- **Pass / fail:** Pass.
- **Evidence:** `T04-20261121-132630.md`

### T05: Multi-item checkout, limit and atomic rollback (API-10, 11)
- **Expected:** subcase 2a (S3 plus E2 through API-10) gives 201; 2b (S1 plus E2 and E6 through API-11) gives 201 with two borrowings; 2c (S3 plus E2 and E6) gives 409 with no partial rows; invalid requests (duplicates, one item, E1 plus E2, S2, missing id, bad date) return 400, 409 or 404 with no writes; an injected failure after the writes were sent and before COMMIT gives 500 and no partial rows.
- **Observed:** steps 1 to 3: 14 of 14 passed (both runs). Step 4 (`EQUIPMENT_BORROWING_FAULT=BeforeCommit`): 500 `database_error` with a safe body; E2 still available; database state unchanged (5 of 5 passed).
- **Documented failure point:** after the INSERT and UPDATE statements for both items were sent inside the transaction, before COMMIT (`EfUnitOfWorkTransaction.CommitAsync` calls `FaultInjection.TriggerBeforeCommit`).
- **Pass / fail:** Pass.
- **Evidence:** `T05-20261121-132721.md`, `T05-20261121-134234.md`, `T05-fault-20261121-133737.md`

### T06: Histories, search, filters and sorting (API-14 to 16)
- **Expected:** 200 arrays or `[]`; 404 for unknown ids; search by partial mixed-case name, student number and equipment name; status filters; all sort orders; combined filters; clearing the criteria restores the default (All, borrowedDate, asc); 400 for unsupported status, sort and direction; no database change.
- **Observed:** 42 of 42 passed.
- **Pass / fail:** Pass.
- **Evidence:** `T06-20261121-134007.md`

### After restart (supports T04 and T08)
- **Expected:** data committed by the previous write test is still readable after the API is restarted.
- **Observed:** 4 of 4 passed.
- **Pass / fail:** Pass.
- **Evidence:** `AfterRestart-20261121-134430.md`

### T07: The desktop is an HTTP client (architecture)
- **Expected:** the desktop works through the API only; changes made through an independent HTTP client appear in the desktop after a refresh; the desktop has no database access.
- **Observed:**
  1. The API works alone (T01).
  2. In the Avalonia app I registered student ST-500 and equipment EQ-008, and issued and returned EQ-002 for ST-400. The independent database read (`T07-snapshot.txt`) shows all of it. Borrowing 5 is `Returned` with `DateReturned 2026-11-10` (the API server's clock) and an expected return date of 2026-11-29 (the default offered by the screen).
  3. Through PowerShell I sent `PATCH /api/v1/students/4/eligibility {"isEligible":false}`; the API returned 200 with `isEligible: False` (`screenshots/T07-06-api-independent-patch-console.png`; it also shows my first attempt failing with `route_not_found` because the id variable was empty, see issue 6). After refreshing the desktop, ST-400 Dani Ong is shown as Restricted (`screenshots/T07-02-ui-students-restricted-after-patch.png`) and the database read shows `IsAllowedToBorrow = False`.
  4. The History screen shows S1 (2 records) and E1 (1 record) with the seed values (`screenshots/T07-04-ui-history-student.png`, `screenshots/T07-05-ui-history-equipment.png`).
  5. `Check-DesktopDependencies.ps1` (`T07-dependencies.txt`): no project references, no database packages (direct or transitive), no database assemblies in the build output; `HttpClient` is used in `Api\EquipmentApiClient.cs` and `App.axaml.cs`. Section 3 of that script listed 13 lines; all are ordinary words ("Select a student...", comments, the `Avalonia.Application` namespace) and none is persistence code, because the search pattern is intentionally broad.
  6. The desktop process was started with no database connection setting.
- **Screenshot gaps (honest):** there is no separate still image of the desktop Students list straight after registering ST-500 (ST-500 is visible in `T07-02`, in `T07-01b` and in the database read), and no still image of the issue and return screens (the database read shows the borrowing issued and returned through the UI). Screen recordings of the session exist on my computer but are too large for the repository (up to 1.3 GB) and are not part of the submission.
- **Pass / fail:** Pass.
- **Evidence:** `T07-dependencies.txt`, `T07-snapshot.txt`, `screenshots/T07-01b-api-students-after-ui-register.png`, `T07-02`, `T07-04`, `T07-05`, `T07-06`.

### T08: Unreachable API versus server or database error, and recovery
- **Expected:** step 1: no HTTP response, and the UI says the service cannot be reached; step 2: a safe 500 body, and the UI shows a server error; step 3: 200 after restoring the configuration, and committed data survives a restart; an unknown commit outcome is reported as unknown and checked before retrying.
- **Observed** (verbatim in `T08-console-captures.txt`):
  - **Step 1, API stopped:** `HttpRequestException`: "No connection could be made because the target machine actively refused it. (localhost:5080)". No HTTP status exists.
  - **Step 2, API running with an invalid password:** HTTP 500, body `{"code":"database_error","message":"PostgreSQL rejected the login. Check the username and password in your local connection settings."}`. No credentials, host, connection string or stack trace in the body. No password appeared in the API console log.
  - **Step 3, real configuration restored:** `GET /api/v1/students` returned 200, and the desktop reloaded the equipment list without being restarted (`screenshots/T08-03-ui-recovered-after-restore.png`). I then created borrowing 5 (ST-400 / EQ-002), restarted the API, and the borrowing was still Active through the API and in the database (`T08-snapshot-after-restart.txt`; EQ-002 unavailable; consistency check 0 rows).
  - **Unknown commit (`EQUIPMENT_BORROWING_FAULT=AfterCommit`):** `POST /api/v1/borrowings` for ST-100 / EQ-006 returned 500 `commit_outcome_unknown` ("it is not known whether the change was saved. Nothing was retried. Refresh and check the records before trying again."). A fresh database read afterwards shows borrowing 6 (ST-100 / EQ-006) persisted as Active, EQ-006 unavailable, consistency check 0 rows (`T08-snapshot-after-unknown-commit.txt`). A failure status is not proof of rollback.
- **Not evidenced in the repository:** still images of the desktop's two failure messages (API stopped, and invalid login). I observed them during the session but the only record of them is in screen recordings kept outside the repository. Because of that, these two desktop sub-checks are recorded as **not captured as images**; the API-side behaviour is fully evidenced above.
- **Pass / fail:** Pass for the API and database behaviour (steps 1 to 3 and the unknown commit); the two UI-message sub-checks are not evidenced here.
- **Evidence:** `T08-console-captures.txt`, `T08-snapshot-after-restart.txt`, `T08-snapshot-after-unknown-commit.txt`, `screenshots/T08-03-ui-recovered-after-restore.png`.

## Screenshots (in `docs\evidence\lab5\screenshots\`)

| File | What it shows | Test |
|---|---|---|
| `T-baseline-reset-and-snapshot.png` | `reset` then `snapshot`: the seeded baseline | all |
| `T01-console-run.png` | `Invoke-Acceptance.ps1 -Test T01` console output | T01 |
| `T07-01b-api-students-after-ui-register.png` | API `GET /students` (PowerShell) listing ST-500 after it was registered in the UI | T07 |
| `T07-02-ui-students-restricted-after-patch.png` | Desktop Students list: ST-400 Restricted after the independent PATCH and a refresh; ST-500 listed | T07 |
| `T07-04-ui-history-student.png` | Desktop history for ST-100 (2 records) | T07 |
| `T07-05-ui-history-equipment.png` | Desktop history for EQ-001 (1 record) | T07 |
| `T07-06-api-independent-patch-console.png` | The independent PATCH returning `isEligible: False` | T07 |
| `T08-03-ui-recovered-after-restore.png` | Desktop equipment list loaded again after the configuration was restored | T08 |

## Issues found and corrected during the lab

| # | Issue | Diagnosis | Correction | Retest |
|---|---|---|---|---|
| 1 | The OpenAPI package resolved to 11.0.0 and `AddOpenApi` and `MapOpenApi` were missing | The package major version did not match the .NET 10 runtime | Pinned `Microsoft.AspNetCore.OpenApi` to `10.0.*` (resolved 10.0.15) | Build succeeded; OpenAPI documents API-01 to 16 (T01) |
| 2 | OpenAPI `paths` was empty ("No action descriptors found") | The Controllers folder had not been added to the repository | Added the four controllers | T01 step 18 and the 16 contract checks |
| 3 | `FileNotFoundException` for `Microsoft.EntityFrameworkCore.Relational 10.0.12` at first database use | Infrastructure compiled against 10.0.12, but consumers resolved Relational 10.0.4 through Npgsql (warning MSB3277) | Pinned `Microsoft.EntityFrameworkCore.Relational` to 10.0.12 in the Infrastructure project | `reset` and `snapshot` ran; all tests passed |
| 4 | `42501 permission denied for schema public` on reset | PostgreSQL 15 and newer restrict the `public` schema; the test database had another owner | Made `equipment_app` the owner of the disposable test database | `reset` completed |
| 5 | The execution policy blocked `Invoke-Acceptance.ps1` | Unsigned local script | `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass` (current window only) | T01 to T06 ran |
| 6 | A PowerShell one-liner produced an empty student id and the API answered `route_not_found` | `Invoke-RestMethod` returned the array as one object. Not an API defect: the API correctly rejected the empty id | Unrolled the array | The PATCH returned 200 |

## Limitations

- The API and database evidence is scripted and repeatable. The desktop evidence is by screenshot and manual observation.
- Only the disposable test database was used; no shared database was interrupted.
- Screen recordings exist but are not in the repository because of their size.

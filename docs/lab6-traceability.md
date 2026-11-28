# Laboratory Activity 6: traceability and baseline record

Companion to the ITSD81 Lab 6 Test Case Suite. The Lab 4 Use Case Suite and the Lab 5 API Reference remain
authoritative; nothing in them was changed. Results in this file come from **one real run on 2026-11-28**
(`dotnet test`, 157 tests executed, 157 passed, 0 failed). A scenario ID is a requirement, not proof of execution:
the "Result" columns below are taken from that run's per-test outcomes.

Evidence for every row: `docs/evidence/lab6/test-run-2026-11-28.txt` (summary) and
`docs/evidence/lab6/lab6-final-trx.xml` (raw per-test results, saved as `lab6-final-trx.xml` because `.trx` files are git-ignored).

---

## 1. Implementation baseline record

| Field | Entry |
|---|---|
| Authentic Lab 5 submitted commit / tag | Tag `lab5-submitted` (annotated) pointing at commit `a1ab4464575c39e72c2585494b979ba52c6c5d45`. The tag object is `9055dcd0f2a4bbaef1fdac1cd7bd93cba61baf87`. `lab4-submitted` and `lab4-final` also exist. |
| How the baseline was verified; missing history or limitations | On 2026-11-22, before any Lab 6 change, `git status` was clean and `git rev-list -n 1 lab5-submitted` equalled `HEAD` (`a1ab446`). No history is missing and nothing was fabricated. Limitation: none known. |
| Final Lab 6 commit and `lab6-submitted` tag | The final Lab 6 revision is the annotated tag `lab6-submitted` on `main`. Its commit hash is shown by `git rev-list -n 1 lab6-submitted` (a commit cannot record its own hash, so the tag is the reference). Test results in this file were taken on 2026-11-28 at the commit titled "test(lab6): add MA6-01/02 manual check record, OpenAPI and desktop-boundary tests, evidence", which is the last commit that changed code; every later commit adds documentation only. |
| Test project(s), isolated PostgreSQL setup, fixture/reset and D | One xUnit project, `tests/EquipmentBorrowing.Tests` (xUnit 2.9.3, `Microsoft.AspNetCore.Mvc.Testing 10.0.*`). Database: the disposable PostgreSQL database `equipment_borrowing_test`, selected only through the environment variable `EQUIPMENT_BORROWING_CONNECTION` (never `appsettings.Local.json`; the tests refuse any database whose name does not end in `_test`). Every test calls `TestDatabase.ResetAsync()` first: `TRUNCATE ... RESTART IDENTITY`, then the fixture S1 to S4 and E1 to E7. D = **2026-11-10**, fixed through `EQUIPMENT_BORROWING_FIXED_DATE`. Persistence checks use fresh connections (pooling off). Tests run serially because they share one database and one process-wide fault switch. |
| Significant refactoring and reproducible execution | (1) `public partial class Program { }` added to the API so `WebApplicationFactory<Program>` can host it. (2) Defect `D-1` fixed in `Program.cs` (`SuppressMapClientErrors`). (3) The unmodified desktop client sources (`ApiResult.cs`, `ApiContracts.cs`, `IEquipmentApi.cs`, `EquipmentApiClient.cs`) are compiled into the test project by linked includes. To reproduce: set `EQUIPMENT_BORROWING_CONNECTION` to the `*_test` database, then run `dotnet test tests\EquipmentBorrowing.Tests` from the repository root. |

---

## 2. Scenario index: TC6 case to implementation and result

"Cases" counts every executed test case (each `InlineData` row is one case). Filter one scenario with
`dotnet test --filter "TC6=U01"` (use `TC6~E07` for the three E07 parts).

| TC6 case | Test class (`tests/EquipmentBorrowing.Tests/UseCases/`) | Methods | Cases | Passed |
|---|---|---|---|---|
| U01 Catalog | `CatalogTests` | 2 | 2 | 2 |
| U02 Available filtering | `CatalogTests` | 2 | 2 | 2 |
| U03 Single borrowing | `BorrowAndReturnTests` | 1 | 1 | 1 |
| U04 Active borrowings | `BorrowingSearchTests` | 1 | 1 | 1 |
| U05 Return and history | `BorrowAndReturnTests` | 3 | 3 | 3 |
| U06 Atomic multi-item success | `CheckoutTests` | 2 | 2 | 2 |
| U07 Student history | `HistoryTests` | 4 | 4 | 4 |
| U08 Equipment history | `HistoryTests` | 4 | 4 | 4 |
| U09 Search and ordering | `BorrowingSearchTests` | 8 | 24 | 24 |
| U12 Register student | `StudentManagementTests` | 2 | 2 | 2 |
| U13 Update student | `StudentManagementTests` | 2 | 2 | 2 |
| U14 Eligibility transitions | `EligibilityTests` | 4 | 4 | 4 |
| U15 Register equipment | `EquipmentManagementTests` | 2 | 2 | 2 |
| U16 Update equipment | `EquipmentManagementTests` | 2 | 2 | 2 |
| E01 Normalized uniqueness | `UniquenessTests` | 8 | 23 | 23 |
| E02 Limit boundaries | `LimitTests` | 6 | 6 | 6 |
| E03 Protocol errors | `ProtocolTests` | 6 | 34 | 34 |
| E04 PostgreSQL rollback | `RollbackTests` | 4 | 4 | 4 |
| E05 Stale and conflicting state | `StaleStateTests` | 6 | 6 | 6 |
| E06 Optional and protected fields | `ProtectedFieldTests` | 8 | 10 | 10 |
| E07 Transport and uncertain outcome | `TransportTests` (E07a 4, E07b 1, E07c 2) | 7 | 7 | 7 |
| E08 Fresh connections and restart | `PersistenceTests` | 2 | 2 | 2 |
| MA6-02 (automated support) | `OpenApiContractTests` (3), `DesktopBoundaryTests` (3) | 6 | 6 | 6 |
| MA6-01 | manual only (no automation, by design) | n/a | n/a | see `docs/lab6-manual-checks.md` |
| **Total TC6-mapped** | | | **153** | **153** |

The other 4 of the 157 executed tests are `InfrastructureSmokeTests` (they check the harness itself: dataset
reset, repeatability, the real HTTP pipeline and ID resolution). They are **not** TC6 cases and are not counted in the matrix.

---

## 3. Requirements traceability matrix (UC to BR to API to TC6)

Scenario results are written as `passed/cases`. All entries below passed in the 2026-11-28 run.

| UC | BR | API | TC6 | Actual test implementation | Execution result | Evidence |
|---|---|---|---|---|---|---|
| UC-01 | BR-05 to 07, 10, 19 | API-01 | U01, E07 | `CatalogTests`; `TransportTests` (`E07a` read double and closed-port tests cover the catalog read) | U01 2/2; E07 7/7 | `test-run-2026-11-28.txt`, `lab6-final-trx.xml` |
| UC-02 | BR-10, 19 | API-01 | U02 | `CatalogTests` | U02 2/2 | same |
| UC-03 | BR-08 to 11, 14 to 16, 19 | API-10, 13 | U03, E02, E03, E05, E07, E08 | `BorrowAndReturnTests`; `LimitTests`; `ProtocolTests`; `StaleStateTests`; `TransportTests`; `PersistenceTests` | U03 1/1; E02 6/6; E03 34/34; E05 6/6; E07 7/7; E08 2/2 | same |
| UC-04 | BR-14, 19 | API-14 | U04, U09 | `BorrowingSearchTests` | U04 1/1; U09 24/24 | same |
| UC-05 | BR-10, 13 to 14, 19 | API-12, 13 | U05, U14, E08 | `BorrowAndReturnTests`; `EligibilityTests`; `PersistenceTests` | U05 3/3; U14 4/4; E08 2/2 | same |
| UC-06 | BR-08 to 12, 14 to 16, 19 | API-11 | U06, E02 to E05, E07 | `CheckoutTests`; `LimitTests`; `ProtocolTests`; `RollbackTests`; `StaleStateTests`; `TransportTests` | U06 2/2; E02 6/6; E03 34/34; E04 4/4; E05 6/6; E07 7/7 | same |
| UC-07 | BR-14, 19 | API-03, 15 | U07 | `HistoryTests` | U07 4/4 | same |
| UC-08 | BR-14, 19 | API-07, 16 | U08, U16 | `HistoryTests`; `EquipmentManagementTests` | U08 4/4; U16 2/2 | same |
| UC-09 | BR-17, 19 | API-14 | U09, E03 | `BorrowingSearchTests`; `ProtocolTests` | U09 24/24; E03 34/34 | same |
| UC-12 | BR-01 to 02, 16, 19 | API-03, 04 | U12, E01, E08 | `StudentManagementTests`; `UniquenessTests`; `PersistenceTests` | U12 2/2; E01 23/23; E08 2/2 | same |
| UC-13 | BR-01 to 03, 14, 16, 19 | API-02, 03, 05, 15 | U13, E01, E06 | `StudentManagementTests`; `UniquenessTests`; `ProtectedFieldTests` | U13 2/2; E01 23/23; E06 10/10 | same |
| UC-14 | BR-04, 08, 13 to 14, 19 | API-06, 10, 12 | U14 | `EligibilityTests` | U14 4/4 | same |
| UC-15 | BR-05 to 06, 10, 16, 19 | API-07, 08 | U15, E01, E08 | `EquipmentManagementTests`; `UniquenessTests`; `PersistenceTests` | U15 2/2; E01 23/23; E08 2/2 | same |
| UC-16 | BR-05 to 07, 10, 14, 16, 19 | API-07, 09, 16 | U16, E01, E06 | `EquipmentManagementTests`; `UniquenessTests`; `ProtectedFieldTests` | U16 2/2; E01 23/23; E06 10/10 | same |

Manual acceptance across UC-01 to 09 and UC-12 to 16: `MA6-01` and `MA6-02`, recorded in `docs/lab6-manual-checks.md`
with screenshots in `docs/evidence/lab6/screenshots/`.

---

## 4. API coverage check (every contract through the real HTTP pipeline)

All automated scenarios use `WebApplicationFactory<Program>` (the real ASP.NET Core pipeline) with the real
PostgreSQL test database, except the E07a transport doubles, which are documented as client-only checks.

| API | Automated scenario(s) | Actual HTTP test class(es) | Result (all scenario cases passed) |
|---|---|---|---|
| API-01 | U01, U02 | `CatalogTests` | U01 2/2; U02 2/2 |
| API-02 | U13 | `StudentManagementTests` (`U13_Search_finds_S1_by_number...`) | U13 2/2 |
| API-03 | U07, U12, U13 | `HistoryTests`, `StudentManagementTests` | U07 4/4; U12 2/2; U13 2/2 |
| API-04 | U12, E01 | `StudentManagementTests`, `UniquenessTests` | U12 2/2; E01 23/23 |
| API-05 | U13, E01, E06 | `StudentManagementTests`, `UniquenessTests`, `ProtectedFieldTests` | U13 2/2; E01 23/23; E06 10/10 |
| API-06 | U14 | `EligibilityTests` | U14 4/4 |
| API-07 | U08, U15, U16 | `HistoryTests`, `EquipmentManagementTests` | U08 4/4; U15 2/2; U16 2/2 |
| API-08 | U15, E01 | `EquipmentManagementTests`, `UniquenessTests` | U15 2/2; E01 23/23 |
| API-09 | U16, E01, E06 | `EquipmentManagementTests`, `UniquenessTests`, `ProtectedFieldTests` | U16 2/2; E01 23/23; E06 10/10 |
| API-10 | U03, U14, E02, E03, E05, E07, E08 | `BorrowAndReturnTests`, `EligibilityTests`, `LimitTests`, `ProtocolTests`, `StaleStateTests`, `TransportTests`, `PersistenceTests` | U03 1/1; U14 4/4; E02 6/6; E03 34/34; E05 6/6; E07 7/7; E08 2/2 |
| API-11 | U06, E02 to E05, E07 | `CheckoutTests`, `LimitTests`, `ProtocolTests`, `RollbackTests`, `StaleStateTests`, `TransportTests` | U06 2/2; E02 6/6; E03 34/34; E04 4/4; E05 6/6; E07 7/7 |
| API-12 | U05, U14, E08 | `BorrowAndReturnTests`, `EligibilityTests`, `PersistenceTests` | U05 3/3; U14 4/4; E08 2/2 |
| API-13 | U03, U05, E08 | `BorrowAndReturnTests`, `PersistenceTests` | U03 1/1; U05 3/3; E08 2/2 |
| API-14 | U04, U09, E03 | `BorrowingSearchTests`, `ProtocolTests` | U04 1/1; U09 24/24; E03 34/34 |
| API-15 | U07, U13 | `HistoryTests`, `StudentManagementTests` | U07 4/4; U13 2/2 |
| API-16 | U08, U16 | `HistoryTests`, `EquipmentManagementTests` | U08 4/4; U16 2/2 |

OpenAPI consistency with API-01 to 16 (rubric R2): `OpenApiContractTests` (3 tests, all passed), plus the live
download in `MA6-02` step 1 (`GET /openapi/v1.json` returned HTTP 200, 58630 bytes).

---

## 5. Notes and limits

- **What the counts mean.** "Cases" counts test cases, not distinct requirements. One scenario's cases may touch
  several APIs, so the per-API results above show the scenarios that cover each API, not a per-API tally.
- **Specification ambiguity (documented, not changed):** the contract says a stable borrowing ID breaks date ties
  but not whether the tie-break follows the sort direction. The implementation orders ties by ID in the same direction as
  the date sort, and `TC6-U09` asserts exactly that rule (see the note in `BorrowingSearchTests.cs`).
- **Beyond the published contract:** a non-JSON request body returns 415. The Lab 5 reference does not list 415; `TC6-E03`
  holds it to the same `{ code, message }` error shape (see defect `D-1`).
- **Test doubles.** The E07a doubles (`ThrowingHandler`, `HangingHandler`) and the lost-response double
  (`LoseResponseHandler`) prove only how the desktop client reacts. All database claims in E04, E05, E07b, E07c and E08 use genuine
  PostgreSQL and fresh independent connections. Limits are written on each double in `Infrastructure/TransportDoubles.cs`.
- **Restart verification (E08):** done with a documented integration harness (`Infrastructure/ApiProcess.cs`), which
  runs the actual API as a separate process and stops and restarts it. No manual restart was needed.
- **Not covered by automation (by design):** `MA6-01` (desktop responsiveness) is manual; no UI automation was written.

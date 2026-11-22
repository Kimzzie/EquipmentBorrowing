# Lab 5 Implementation Baseline Record

Author: Kimberly Bautista, 2023303116 (individual, no partner).

| Field | Entry |
|---|---|
| Lab 4 submitted commit | `d23dd40714eb0d7ad20503975b808e9f6e2728b8` ("docs: list sole author and finalize A19 review", authored 2026-11-19). This is the final commit of Lab 4 and the `HEAD` of `main` before any Lab 5 work began. Identified with `git rev-list -n 1 lab4-submitted` and `git rev-list -n 1 lab4-final`, which both print this hash. |
| Lab 4 tags | `lab4-final` (lightweight tag) and `lab4-submitted` (annotated tag). `lab4-submitted` was created on 2026-11-21, after the fact, on the genuine final Lab 4 commit above; the commit itself was not changed. |
| Baseline limitations | None known: the original commit exists and is verifiable. Lab 4 verification was manual (`docs/evidence.md`); there were no automated tests for it. |
| Lab 5 revision and tag | Implementation commits start after `d23dd40`. The completed revision is the commit that carries the `lab5-submitted` tag: `git rev-list -n 1 lab5-submitted`. Last implementation commit before the tag: `bb44dc4d22d8ccafe21590b30cde7737e3ea134a`. |
| Before boundary | Lab 4: the Avalonia app referenced Application and Infrastructure directly, composed EF Core and Npgsql in `App.axaml.cs`, and ran migrations and seeding at startup (`IDatabaseInitializer`). The desktop opened its own database connections. |
| After boundary | Lab 5: **Desktop** (Avalonia, views and view models) has no project references and no database packages; it only uses `HttpClient` through `Api/EquipmentApiClient.cs`. **Api** (`src/EquipmentBorrowing.Api`, ASP.NET Core controllers) maps HTTP to the Application services and errors to `{code,message}`. **Application** keeps the use-case services and business rules. **Infrastructure** keeps EF Core, Npgsql, migrations and repositories. **Domain** is unchanged. Only the API process holds the database connection. |
| Migrations | None added in Lab 5. The only migration is still `InitialPostgreSql` (`20261112155340`). The API does not run migrations on startup; the schema is created with `dotnet ef database update` or, for the disposable test database, `tools/EquipmentBorrowing.TestData reset`. Data relationships are unchanged (same tables, foreign keys, constraints and the single-active-borrowing partial unique index). |
| Reproduction | See the "Laboratory Activity 5" section of `README.md` (setup, API base URL, data reset, how to run each acceptance test, where evidence is). |

## Justified refactoring (changes to Lab 4 code)

| Change | Why it was needed |
|---|---|
| New project `src/EquipmentBorrowing.Api` | The lab requires a REST API (API-01 to API-16, `/api/v1`, OpenAPI at `/openapi/v1.json`, `{code,message}` errors). |
| New read services `GetBorrowingService`, `GetEquipmentService`, `GetStudentService`; `IBorrowingQueryRepository` gained a get-by-id method (`EfBorrowingQueryRepository`) | API-03, 07 and 13 return one record by id, which Lab 4 had no use case or query for. |
| Desktop `Api/` folder (`IEquipmentApi`, `EquipmentApiClient`, `ApiResult`, contracts) and new desktop models | The desktop must use HTTP, with no direct database access, and tell "API unreachable" (no response) from a received 500. |
| All view models rewritten to call the API asynchronously; `Desktop.csproj` stripped of project references and EF packages | Remove direct persistence access; keep the UI responsive; show reasons for rejections and refresh after a failure (BR-18). |
| Equipment text search is done in the client | API-01 only has the `available` filter. |
| Active Borrowings sorts ascending by borrowed date | API-14 default sort is `borrowedDate` ascending; the client sends `direction=desc` explicitly where a screen wants newest first. |
| Write requests use `CancellationToken.None` | A client disconnect must not abort a commit that is in progress. |
| `Microsoft.EntityFrameworkCore.Relational` pinned to 10.0.12 in Infrastructure | Fixes a runtime version mismatch (see issue 3 in `docs/acceptance/test-results.md`). |
| `tools/EquipmentBorrowing.TestData` and `docs/acceptance/*.ps1` | Repeatable seed data, an independent database snapshot, and the acceptance runner. The tool refuses any database whose name does not end in `_test`. |
| `EquipmentBorrowing.slnx` | Added the Api and TestData projects; corrected a broken file path that pointed outside the repository. |

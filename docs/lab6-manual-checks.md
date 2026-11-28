# Laboratory Activity 6: manual acceptance checks (MA6-01 and MA6-02)

These two checks are done by hand on purpose (the handout asks for no UI automation). Everything that **can**
be automated for them already is. The "Observed" cells below record what the student saw while running the steps
(run on 2026-11-28 with screenshots). Cells still marked **(fill in)** have not been observed yet.

Date run: **2026-11-28**   Computer: **ZIE** (Windows, PowerShell 7)   API commit: **ef5fa11** (the Stage 9 helper files were still uncommitted)
Test database: `equipment_borrowing_test`   Fixed date D: `2026-11-10`

---

## MA6-01: Desktop responsiveness

**Objective:** record observed UI behaviour while the API is slow, stopped and restored. No UI automation.
**Related:** UC-01 to 09, 12 to 16; BR-18, 19; API-01 to 16.

### Setup (three PowerShell windows, repo root)

```powershell
# Window 1: reset the disposable data, then run the API with the fixed clock
$env:EQUIPMENT_BORROWING_FIXED_DATE = "2026-11-10"
dotnet run --project tools\EquipmentBorrowing.TestData -- reset      # (same reset command as Lab 5)
dotnet run --project src\EquipmentBorrowing.Api --urls http://localhost:5080

# Window 2: the delaying proxy (changes nothing in the API or desktop)
.\docs\acceptance\Start-SlowProxy.ps1 -DelaySeconds 8

# Window 3: the desktop, pointed at the PROXY
$env:EQUIPMENT_BORROWING_API_URL = "http://localhost:5081"
dotnet run --project src\EquipmentBorrowing.Desktop
```

### Steps and observations

| # | Do this | Expected | Observed |
|---|---|---|---|
| 1 | Open the Equipment screen (proxy delays 8 s). | A loading indicator is visible; the list appears after about 8 s. | "Loading equipment..." with a progress bar was shown (Search Borrowings showed "Loading borrowings..." the same way, with the Search button disabled). The proxy log shows each request "held 8 s" and the list appeared afterwards. |
| 2 | During the 8 s wait, drag the window, resize it, click another tab or button. | The window keeps responding; it does not freeze or show "Not responding". | Yes: the window could be dragged, resized and clicked during the wait. No freeze. |
| 3 | Open Borrowings and press Search while a load is in progress. | Another loading state; the first screen's data is not mixed in; no crash. | Pressed Search while a load was running. No crash. The Search button was disabled during the load. The previous results stayed visible under "Loading borrowings..." until the new load finished (one screenshot shows "Loading borrowings..." and "4 record(s) found" together). |
| 4 | Stop the proxy **and** the API (Ctrl+C in windows 1 and 2), then press Retry or reopen a screen. | A clear message that the service **cannot be reached** (no HTTP response). It must NOT look like "no results". | With the API and proxy stopped, Active Borrowings and Search Borrowings showed in red: "Cannot reach the Equipment Borrowing service at http://localhost:5081/. Check that the API is running, then try again." with a Retry button; neither looked like an empty list. Starting the desktop while the service was down showed "Cannot start the application" with the same message and Retry. |
| 5 | Start the API again (window 1) and re-point the desktop at it if needed, then press Retry. | The list loads again **without restarting the desktop**. | First attempt: the desktop was restarted (it had been stopped with Ctrl+C), and its startup screen "Connecting to the service..." then loaded data (startup recovery only). Repeated in the same open desktop window without restarting it: after the proxy and API were restarted, searching worked again. "kim" gave "No borrowings match these criteria." and "Lim" returned 2 records (ST-300 Cara Lim: EQ-005 Microphone and EQ-004 Tablet, both Active). |
| 6 | (Optional) Restart the proxy with `-DelaySeconds 20` and open a screen. | After about 15 s: "did not respond in time" (the client's own timeout), still no freeze. | Not run (optional). Covered by automated `TC6-E07a` (hanging-server double). |
| 7 | With the API running, arm the fault: `$env:EQUIPMENT_BORROWING_FAULT = "BeforeCommit"` in window 1, restart the API, try a checkout in the desktop. | A message that the service **is running but reported an error**, which reads differently from step 4. | Checkout of Laptop + Tripod for Dani Ong showed in red: "The service is running but reported an error. SIMULATED failure after the writes were sent but before the commit. The transaction was not committed; nothing was saved." Different wording from step 4. The student, return date and checkout list stayed filled in. ("SIMULATED" comes from the test-only fault hook, which only works on a `*_test` database.) |
| 8 | Read the database afterwards (see `docs/acceptance/snapshot.sql`). | No unintended writes: the same rows as before step 7. | `snapshot` on `equipment_borrowing_test`: 4 students, 7 equipment (EQ-002 Laptop and EQ-006 Tripod still Available), 4 borrowings (ids 1 to 4, unchanged), consistency check 0 rows. Identical to the starting data: nothing was saved. |

**Result:** Pass for steps 1 to 5, 7 and 8. Step 6 is optional and was not run.
Note: the helper proxy script (`Start-SlowProxy.ps1`) stopped once with `$context.Response.Close()` ("nonexistent network connection"), after the desktop abandoned a request that was being held. It was restarted. This is a limitation of the helper script, not of the application.
**Evidence:** the MA6-01 screenshots (taken 16:39 to 17:26 on 2026-11-28) are in `docs/evidence/lab6/screenshots/` and listed in the gallery at the end of this file. The proxy log lines "held 8 s" visible in several of them show the delay was real.

> Automated support (already done, for your cross-reference): `TC6-E07a/b/c` prove, with the same
> `EquipmentApiClient` source the desktop uses, that "no response" and "a received 500" are different
> failures and that an unknown outcome is never retried.

---

## MA6-02: Independent API and synchronization

**Objective:** show the API works without the desktop, the OpenAPI description is consistent, a change made by
an independent client shows up in the desktop after a refresh, and the desktop is HTTP-only.
**Related:** UC-01 to 09, 12 to 16; BR-14, 18, 19; API-01 to 16.

### Already evidenced automatically

| Part | Evidence |
|---|---|
| API runs without the desktop | `TC6-E08` restart harness starts the API as a bare process and calls it over HTTP; the Lab 5 `T01` did the same. |
| OpenAPI consistent with API-01 to 16 | xUnit `OpenApiContractTests` (3 tests) and `docs/evidence/lab6/MA6-02-openapi-operations.txt`. |
| Desktop has no database access | `docs/evidence/lab6/MA6-02-dependencies.txt` (output of `Check-DesktopDependencies.ps1`) and xUnit `DesktopBoundaryTests` (3 tests). |

### Steps run by hand

```powershell
# Window 1: API only. NO desktop running yet.
dotnet run --project src\EquipmentBorrowing.Api --urls http://localhost:5080
# Window 2: independent HTTP client (PowerShell)
Invoke-RestMethod http://localhost:5080/api/v1/equipment
```

| # | Do this | Expected | Observed |
|---|---|---|---|
| 1 | With only the API running, call `GET /api/v1/equipment` and `GET /openapi/v1.json`. | 200 with the seven items; the OpenAPI document downloads. | The desktop was not running. `GET /api/v1/equipment` returned the 7 items EQ-001 to EQ-007 with the expected availability (EQ-001, EQ-004, EQ-005 not available). `GET /openapi/v1.json` returned HTTP 200, 58630 bytes (the same size as the saved `openapi-v1.json`). |
| 2 | Start the desktop (`$env:EQUIPMENT_BORROWING_API_URL = "http://localhost:5080"`) and open Students. Note ST-400's status. | Dani Ong is **Eligible**. | Students screen: Dani Ong (ST-400) was Eligible (Ada Cruz and Cara Lim Eligible, Ben Diaz Restricted). A `snapshot` taken at this point showed ST-400 `IsAllowedToBorrow = True`. |
| 3 | From PowerShell: `Invoke-RestMethod -Method Patch -Uri http://localhost:5080/api/v1/students/<id of ST-400>/eligibility -ContentType application/json -Body '{"isEligible":false}'` | 200, `isEligible` false. | The response showed id 4, ST-400, Dani Ong, `isEligible : False`. (The same request was sent twice; the second was a harmless no-op.) |
| 4 | In the desktop press Refresh (do not restart it). | Dani Ong now shows **Restricted**: the change came through the API. | After refreshing in the already-open desktop, Dani Ong shows **Restricted** in red. Refresh method used: **(fill in: Refresh button, or left the screen and came back)**. |
| 5 | Read the database independently (`docs/acceptance/snapshot.sql`). | `IsAllowedToBorrow = false` for ST-400: **one** persisted change, seen by both clients. | `snapshot` after the PATCH: ST-400 `IsAllowedToBorrow = False`. Everything else unchanged compared with the earlier snapshot: 4 students, 7 equipment rows with the same availability, 4 borrowings (ids 1 to 4), consistency check 0 rows. |
| 6 | Run `.\docs\acceptance\Check-DesktopDependencies.ps1` and read sections 1 to 4. | No project references, no database packages or assemblies. | After `Unblock-File` (PowerShell blocked the downloaded script as unsigned): section 1 no project-to-project references; section 2 `none`; section 4 `none`. Section 3 listed 13 lines, all reviewed: the English word "select" in UI text and comments (for example "Please select a student.") and Avalonia's own `Application` class. None is database access. Section 5 shows `HttpClient` only in `Api/EquipmentApiClient.cs` and its registration in `App.axaml.cs`. |
| 7 | (Extra, not in the original steps.) Reset the test data and refresh the open desktop. | Dani Ong is Eligible again: the desktop follows the database in both directions. | After `dotnet run --project tools\EquipmentBorrowing.TestData -- reset` and a refresh of the open desktop, Students shows Dani Ong (ST-400) **Eligible** again (Ben Diaz Restricted, as seeded). |

**Result:** Pass. Step 4 still needs the refresh method written in.
**Evidence:** the MA6-02 screenshots (taken 17:32 to 17:42 on 2026-11-28) are in `docs/evidence/lab6/screenshots/` and listed in the gallery at the end of this file. Today's dependency-check output is saved as `docs/evidence/lab6/MA6-02-dependencies-2026-11-28.txt`.

---

## Screenshot gallery (chronological)

All screenshots were taken by the student on 2026-11-28 and copied unchanged (spaces in file names replaced by dashes). Captions give the time taken; what each check shows is described in the observation tables above. MA6-01 covers up to 17:26 and MA6-02 from 17:32.

### MA6-01 (desktop responsiveness)

**16:39:56**

![MA6-01 screenshot 16:39:56](evidence/lab6/screenshots/Screenshot-2026-11-28-163956.png)

**16:40:00**

![MA6-01 screenshot 16:40:00](evidence/lab6/screenshots/Screenshot-2026-11-28-164000.png)

**16:40:49**

![MA6-01 screenshot 16:40:49](evidence/lab6/screenshots/Screenshot-2026-11-28-164049.png)

**16:43:16**

![MA6-01 screenshot 16:43:16](evidence/lab6/screenshots/Screenshot-2026-11-28-164316.png)

**16:43:52**

![MA6-01 screenshot 16:43:52](evidence/lab6/screenshots/Screenshot-2026-11-28-164352.png)

**16:45:15**

![MA6-01 screenshot 16:45:15](evidence/lab6/screenshots/Screenshot-2026-11-28-164515.png)

**16:46:53**

![MA6-01 screenshot 16:46:53](evidence/lab6/screenshots/Screenshot-2026-11-28-164653.png)

**16:49:15**

![MA6-01 screenshot 16:49:15](evidence/lab6/screenshots/Screenshot-2026-11-28-164915.png)

**16:50:28**

![MA6-01 screenshot 16:50:28](evidence/lab6/screenshots/Screenshot-2026-11-28-165028.png)

**16:50:51**

![MA6-01 screenshot 16:50:51](evidence/lab6/screenshots/Screenshot-2026-11-28-165051.png)

**16:51:18**

![MA6-01 screenshot 16:51:18](evidence/lab6/screenshots/Screenshot-2026-11-28-165118.png)

**16:51:24**

![MA6-01 screenshot 16:51:24](evidence/lab6/screenshots/Screenshot-2026-11-28-165124.png)

**17:07:33**

![MA6-01 screenshot 17:07:33](evidence/lab6/screenshots/Screenshot-2026-11-28-170733.png)

**17:07:50**

![MA6-01 screenshot 17:07:50](evidence/lab6/screenshots/Screenshot-2026-11-28-170750.png)

**17:11:41**

![MA6-01 screenshot 17:11:41](evidence/lab6/screenshots/Screenshot-2026-11-28-171141.png)

**17:13:01**

![MA6-01 screenshot 17:13:01](evidence/lab6/screenshots/Screenshot-2026-11-28-171301.png)

**17:13:08**

![MA6-01 screenshot 17:13:08](evidence/lab6/screenshots/Screenshot-2026-11-28-171308.png)

**17:17:26**

![MA6-01 screenshot 17:17:26](evidence/lab6/screenshots/Screenshot-2026-11-28-171726.png)

**17:18:29**

![MA6-01 screenshot 17:18:29](evidence/lab6/screenshots/Screenshot-2026-11-28-171829.png)

**17:20:24**

![MA6-01 screenshot 17:20:24](evidence/lab6/screenshots/Screenshot-2026-11-28-172024.png)

**17:24:44**

![MA6-01 screenshot 17:24:44](evidence/lab6/screenshots/Screenshot-2026-11-28-172444.png)

**17:24:54**

![MA6-01 screenshot 17:24:54](evidence/lab6/screenshots/Screenshot-2026-11-28-172454.png)

**17:25:09**

![MA6-01 screenshot 17:25:09](evidence/lab6/screenshots/Screenshot-2026-11-28-172509.png)

**17:26:06**

![MA6-01 screenshot 17:26:06](evidence/lab6/screenshots/Screenshot-2026-11-28-172606.png)

### MA6-02 (independent API and synchronization)

**17:32:07**

![MA6-02 screenshot 17:32:07](evidence/lab6/screenshots/Screenshot-2026-11-28-173207.png)

**17:33:32**

![MA6-02 screenshot 17:33:32](evidence/lab6/screenshots/Screenshot-2026-11-28-173332.png)

**17:34:14**

![MA6-02 screenshot 17:34:14](evidence/lab6/screenshots/Screenshot-2026-11-28-173414.png)

**17:35:17**

![MA6-02 screenshot 17:35:17](evidence/lab6/screenshots/Screenshot-2026-11-28-173517.png)

**17:36:06**

![MA6-02 screenshot 17:36:06](evidence/lab6/screenshots/Screenshot-2026-11-28-173606.png)

**17:37:56**

![MA6-02 screenshot 17:37:56](evidence/lab6/screenshots/Screenshot-2026-11-28-173756.png)

**17:38:37**

![MA6-02 screenshot 17:38:37](evidence/lab6/screenshots/Screenshot-2026-11-28-173837.png)

**17:40:39**

![MA6-02 screenshot 17:40:39](evidence/lab6/screenshots/Screenshot-2026-11-28-174039.png)

**17:42:55**

![MA6-02 screenshot 17:42:55](evidence/lab6/screenshots/Screenshot-2026-11-28-174255.png)


<#
.SYNOPSIS
  Runs one Lab 5 acceptance test against the API and writes an evidence file.
.EXAMPLE
  .\docs\acceptance\Invoke-Acceptance.ps1 -Test T04
.NOTES
  Needs: API running with the http-test-clock profile, and EQUIPMENT_BORROWING_CONNECTION
  pointing at the *_test database in THIS window. It resets the data, so it refuses otherwise.
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('T01', 'T02', 'T03', 'T04', 'T05', 'T05-fault', 'T06', 'AfterRestart')]
    [string]$Test,

    [string]$BaseUrl = 'http://localhost:5080'
)

$ErrorActionPreference = 'Stop'
$script:Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

if ($env:EQUIPMENT_BORROWING_CONNECTION -notmatch '(?i)Database=[^;]*_test\s*(;|$)') {
    throw 'Set EQUIPMENT_BORROWING_CONNECTION to the *_test database in this window first (Stage 6a). This script resets data, so it refuses to run otherwise.'
}

Add-Type -AssemblyName System.Net.Http
$script:Http = New-Object System.Net.Http.HttpClient
$script:Http.Timeout = [TimeSpan]::FromSeconds(30)
$script:Log = New-Object System.Collections.Generic.List[string]
$script:Pass = 0
$script:Fail = 0
$script:Step = 0
$script:Tick = [string][char]96
$script:Today = '2026-11-10'
$script:Due = '2026-11-12'
$script:EvidenceDir = Join-Path $script:Root 'docs\evidence\lab5'
$script:LastSnapshotFile = Join-Path $script:EvidenceDir '.last-snapshot.txt'

# ---------------------------------------------------------------- logging helpers

function Add-Log([string]$Line = '') { [void]$script:Log.Add($Line) }

function Add-Block([string]$Text, [string]$Language = '') {
    if ([string]::IsNullOrEmpty($Text)) { $Text = '(empty body)' }
    Add-Log ('```' + $Language)
    Add-Log $Text
    Add-Log '```'
}

function Add-Result([string]$Label, [bool]$Ok, [string]$Detail = '') {
    if ($Ok) { $script:Pass++ } else { $script:Fail++ }
    $mark = if ($Ok) { 'PASS' } else { 'FAIL' }
    Add-Log "**$mark** - $Label $Detail"
    $color = if ($Ok) { 'Green' } else { 'Red' }
    Write-Host "[$mark] $Label $Detail" -ForegroundColor $color
}

function Assert-Equal([string]$Label, $Actual, $Expected) {
    Add-Result $Label ("$Actual" -ceq "$Expected") "(expected '$Expected', observed '$Actual')"
}

# Regex on the RAW response text. Used for dates/status so PowerShell's JSON parser cannot reformat them.
function Assert-Text([string]$Label, [string]$Text, [string]$Pattern) {
    Add-Result $Label ($Text -match $Pattern) "(expected the response to match $Pattern)"
}

function Assert-Same([string]$Label, [string]$A, [string]$B) {
    $same = ($A -ceq $B)
    $detail = if ($same) { '(database state unchanged)' } else { '(database state CHANGED - compare the two snapshots above)' }
    Add-Result $Label $same $detail
}

function Add-Expected([string]$Text) {
    Add-Log ''
    Add-Log "**Expected PostgreSQL state (lab reference) - read the snapshots and record your verdict:** $Text"
}

# ---------------------------------------------------------------- JSON helpers

function Json($Object) { return ($Object | ConvertTo-Json -Compress -Depth 5) }

# Always returns a flat array, also for [] and for one-element arrays, in Windows PowerShell and PowerShell 7.
function ConvertTo-Items([string]$Text) {
    $parsed = ConvertFrom-Json $Text
    if ($null -eq $parsed) { return @() }
    return @($parsed)
}

function Join-Field($Items, [string]$Field, [bool]$Sort = $false) {
    $values = @(@($Items) | ForEach-Object { $_.$Field })
    if ($Sort) { $values = @($values | Sort-Object) }
    return ($values -join ',')
}

# ---------------------------------------------------------------- HTTP

function Invoke-Api([string]$Method, [string]$Path, [int]$Expect, [string]$Label, [string]$Body = $null) {
    $script:Step++
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), ($BaseUrl + $Path))
    if ($Body) {
        $request.Content = [System.Net.Http.StringContent]::new($Body, [System.Text.Encoding]::UTF8, 'application/json')
    }

    $sentAt = Get-Date -Format 'HH:mm:ss'
    try {
        $response = $script:Http.SendAsync($request).GetAwaiter().GetResult()
    }
    catch {
        throw "No HTTP response from $BaseUrl (is the API running?): $($_.Exception.GetBaseException().Message)"
    }

    $status = [int]$response.StatusCode
    $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $location = $null
    if ($response.Headers.Location) { $location = $response.Headers.Location.ToString() }

    Add-Log ''
    Add-Log "### Step $($script:Step) - $Label"
    Add-Log ("Request ($sentAt): " + $script:Tick + $Method + ' ' + $Path + $script:Tick)
    if ($Body) { Add-Block $Body 'json' }
    if ($location) { Add-Log "Location: $location" }
    if ($status -eq 405) { Add-Log ('Allow: ' + ($response.Content.Headers.Allow -join ', ')) }
    Add-Log "Response status: $status"
    Add-Block $text 'json'
    Add-Result "Step $($script:Step) $Label" ($status -eq $Expect) "(expected $Expect, observed $status)"

    return [pscustomobject]@{ Status = $status; Text = $text; Location = $location }
}

# Setup reads (resolve ids from the API instead of assuming database keys). Not numbered as steps.
function Get-Json([string]$Path) {
    $response = $script:Http.GetAsync($BaseUrl + $Path).GetAwaiter().GetResult()
    $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) { throw "Setup read $Path failed with $([int]$response.StatusCode): $text" }
    return (ConvertTo-Items $text)
}

function Get-StudentId([string]$Number) {
    foreach ($s in @(Get-Json '/api/v1/students')) {
        if ($s.studentNumber -eq $Number) { Add-Log "Resolved student $Number -> id $($s.id) (GET /api/v1/students)"; return [int]$s.id }
    }
    throw "Student $Number not found. Reset the data first."
}

function Get-EquipmentId([string]$Code) {
    foreach ($e in @(Get-Json '/api/v1/equipment')) {
        if ($e.equipmentCode -eq $Code) { Add-Log "Resolved equipment $Code -> id $($e.id) (GET /api/v1/equipment)"; return [int]$e.id }
    }
    throw "Equipment $Code not found. Reset the data first."
}

function Get-BorrowingId([string]$StudentNumber, [string]$Code) {
    foreach ($b in @(Get-Json '/api/v1/borrowings')) {
        if ($b.studentNumber -eq $StudentNumber -and $b.equipmentCode -eq $Code) {
            Add-Log "Resolved borrowing $StudentNumber/$Code -> id $($b.id) (GET /api/v1/borrowings)"
            return [int]$b.id
        }
    }
    throw "Borrowing $StudentNumber/$Code not found."
}

# GET a list and compare the equipmentCode values it contains.
function Test-CodeList([string]$Label, [string]$Path, [string]$ExpectedCodes, [bool]$IgnoreOrder = $false) {
    $r = Invoke-Api GET $Path 200 $Label
    $items = @(ConvertTo-Items $r.Text)
    Assert-Equal "$Label - equipmentCode values" (Join-Field $items 'equipmentCode' $IgnoreOrder) $ExpectedCodes
}

# ---------------------------------------------------------------- database tool

function Invoke-Tool([string]$Command) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    Push-Location $script:Root
    try {
        $toolArgs = @('run', '--no-build', '--project', 'tools\EquipmentBorrowing.TestData', '--', $Command)
        $output = (& dotnet @toolArgs 2>&1 | Out-String).TrimEnd()
        $exit = $LASTEXITCODE
    }
    finally {
        Pop-Location
        $ErrorActionPreference = $previous
    }
    if ($exit -ne 0) { throw "Tool '$Command' failed (exit $exit): $output" }
    return $output
}

function Reset-Data([string]$Title) {
    Add-Log ''
    Add-Log "## $Title"
    Add-Log "Reset at $(Get-Date -Format 'HH:mm:ss'):"
    Add-Block (Invoke-Tool 'reset')
}

function Save-Snapshot([string]$Name) {
    $text = Invoke-Tool 'snapshot'
    Add-Log ''
    Add-Log "**Database snapshot - $Name** (raw SQL via Npgsql, independent of the API)"
    Add-Block $text
    return $text
}

function Save-LastSnapshot([string]$Text) { Set-Content -Path $script:LastSnapshotFile -Value $Text -Encoding UTF8 }

# ================================================================ T01

function Test-T01 {
    Reset-Data 'T01 - independent reads, no writes'
    $before = Save-Snapshot 'BEFORE'

    $r = Invoke-Api GET '/api/v1/equipment' 200 'API-01 list all equipment'
    $items = @(ConvertTo-Items $r.Text)
    Assert-Equal 'equipment count' $items.Count 7

    Test-CodeList 'API-01 available=true (E1/E4/E5 excluded)' '/api/v1/equipment?available=true' 'EQ-002,EQ-003,EQ-006,EQ-007' $true

    $r = Invoke-Api GET '/api/v1/students' 200 'API-02 list students'
    $items = @(ConvertTo-Items $r.Text)
    Assert-Equal 'student count' $items.Count 4

    $s1 = Get-StudentId 'ST-100'
    $s4 = Get-StudentId 'ST-400'
    $e1 = Get-EquipmentId 'EQ-001'
    $b1 = Get-BorrowingId 'ST-100' 'EQ-001'

    $r = Invoke-Api GET "/api/v1/students/$s1" 200 'API-03 get S1'
    Assert-Equal 'S1 name' (ConvertFrom-Json $r.Text).fullName 'Ada Cruz'

    $r = Invoke-Api GET "/api/v1/equipment/$e1" 200 'API-07 get E1'
    Assert-Equal 'E1 isAvailable' (ConvertFrom-Json $r.Text).isAvailable 'False'

    $r = Invoke-Api GET "/api/v1/borrowings/$b1" 200 'API-13 get known borrowing'
    Assert-Text 'borrowing is Active with related names' $r.Text '"status":"Active".*"studentName":"Ada Cruz"|"studentName":"Ada Cruz".*"status":"Active"'

    Test-CodeList 'API-15 S1 history (active + returned)' "/api/v1/students/$s1/borrowings" 'EQ-001,EQ-003' $true
    Test-CodeList 'API-16 E1 history' "/api/v1/equipment/$e1/borrowings" 'EQ-001'
    Test-CodeList 'API-14 active borrowings' '/api/v1/borrowings?status=Active' 'EQ-001,EQ-004,EQ-005' $true

    $r = Invoke-Api GET "/api/v1/students/$s4/borrowings" 200 'API-15 empty history (S4)'
    Assert-Equal 'empty history body' $r.Text.Trim() '[]'

    $null = Invoke-Api GET '/api/v1/students/99999' 404 'unknown student id'
    $null = Invoke-Api GET '/api/v1/equipment/99999' 404 'unknown equipment id'
    $null = Invoke-Api GET '/api/v1/borrowings/99999' 404 'unknown borrowing id'
    $null = Invoke-Api GET '/api/v1/students/99999/borrowings' 404 'unknown student history'
    $null = Invoke-Api GET '/api/v1/equipment/99999/borrowings' 404 'unknown equipment history'
    $null = Invoke-Api GET '/api/v1/nothing' 404 'wrong route'
    $null = Invoke-Api DELETE "/api/v1/students/$s1" 405 'wrong method'

    # OpenAPI: all 16 contracts present (compare the printed response codes with reference Section 2)
    $r = Invoke-Api GET '/openapi/v1.json' 200 'OpenAPI document'
    $doc = ConvertFrom-Json $r.Text
    $expected = @(
        @('API-01', 'get', '/api/v1/equipment'),
        @('API-02', 'get', '/api/v1/students'),
        @('API-03', 'get', '/api/v1/students/{id}'),
        @('API-04', 'post', '/api/v1/students'),
        @('API-05', 'put', '/api/v1/students/{id}'),
        @('API-06', 'patch', '/api/v1/students/{id}/eligibility'),
        @('API-07', 'get', '/api/v1/equipment/{id}'),
        @('API-08', 'post', '/api/v1/equipment'),
        @('API-09', 'put', '/api/v1/equipment/{id}'),
        @('API-10', 'post', '/api/v1/borrowings'),
        @('API-11', 'post', '/api/v1/checkout-requests'),
        @('API-12', 'post', '/api/v1/borrowings/{id}/return'),
        @('API-13', 'get', '/api/v1/borrowings/{id}'),
        @('API-14', 'get', '/api/v1/borrowings'),
        @('API-15', 'get', '/api/v1/students/{id}/borrowings'),
        @('API-16', 'get', '/api/v1/equipment/{id}/borrowings')
    )
    foreach ($e in $expected) {
        $pathItem = $doc.paths.PSObject.Properties[$e[2]]
        $operation = $null
        if ($pathItem) { $operation = $pathItem.Value.PSObject.Properties[$e[1]] }
        if ($operation) {
            $codes = ($operation.Value.responses.PSObject.Properties.Name) -join ','
            Add-Result "OpenAPI documents $($e[0]) $($e[1].ToUpper()) $($e[2])" $true "(published responses: $codes)"
        }
        else {
            Add-Result "OpenAPI documents $($e[0]) $($e[1].ToUpper()) $($e[2])" $false '(operation missing from the document)'
        }
    }

    $after = Save-Snapshot 'AFTER'
    Assert-Same 'T01 made no database changes' $before $after
}

# ================================================================ T02

function Test-T02 {
    Reset-Data 'T02 - student management and eligibility'
    $before = Save-Snapshot 'BEFORE'
    $s1 = Get-StudentId 'ST-100'
    $e1 = Get-EquipmentId 'EQ-001'
    $e2 = Get-EquipmentId 'EQ-002'
    $b1 = Get-BorrowingId 'ST-100' 'EQ-001'

    # 1. create, follow Location, update
    $created = Invoke-Api POST '/api/v1/students' 201 'API-04 create ST-500 (contact null)' (Json @{ studentNumber = 'ST-500'; fullName = 'Eli Tan'; contact = $null })
    $new = ConvertFrom-Json $created.Text
    Assert-Equal 'Location points at API-03' $created.Location "$BaseUrl/api/v1/students/$($new.id)"
    Assert-Equal 'new student is eligible' $new.isEligible 'True'
    $null = Invoke-Api GET "/api/v1/students/$($new.id)" 200 'API-03 follow Location'
    $null = Invoke-Api PUT "/api/v1/students/$($new.id)" 200 'API-05 change name and contact' (Json @{ studentNumber = 'ST-500'; fullName = 'Eli T. Tan'; contact = '09171234567' })

    # 2. rejected requests
    $null = Invoke-Api POST '/api/v1/students' 400 'blank name' (Json @{ studentNumber = 'ST-501'; fullName = '  '; contact = $null })
    $null = Invoke-Api POST '/api/v1/students' 409 'duplicate " st-100 " (trim + case-insensitive)' (Json @{ studentNumber = ' st-100 '; fullName = 'Copy'; contact = $null })

    # 3. eligibility flow
    $null = Invoke-Api PATCH "/api/v1/students/$s1/eligibility" 200 'API-06 restrict S1' (Json @{ isEligible = $false })
    Test-CodeList 'S1 history is intact while restricted' "/api/v1/students/$s1/borrowings" 'EQ-001,EQ-003' $true
    $null = Invoke-Api POST '/api/v1/borrowings' 409 'new issue to restricted S1' (Json @{ studentId = $s1; equipmentId = $e2; expectedReturnDate = $script:Due })
    $null = Invoke-Api POST "/api/v1/borrowings/$b1/return" 200 'restricted S1 can still return E1' '{}'
    $null = Invoke-Api PATCH "/api/v1/students/$s1/eligibility" 200 'permit S1 again' (Json @{ isEligible = $true })
    $null = Invoke-Api POST '/api/v1/borrowings' 201 'valid issue after permit' (Json @{ studentId = $s1; equipmentId = $e2; expectedReturnDate = $script:Due })
    $null = Invoke-Api PATCH "/api/v1/students/$s1/eligibility" 200 'same eligibility again (no-op)' (Json @{ isEligible = $true })

    $after = Save-Snapshot 'AFTER'
    Save-LastSnapshot $after
    Add-Expected 'exactly one new student (ST-500, name and contact edited); no row for the blank or duplicate attempts; S1 keeps its returned E3 record, E1 now returned (available), and a new active record for E2; consistency check shows 0 rows.'
}

# ================================================================ T03

function Test-T03 {
    Reset-Data 'T03 - equipment management and protected state'
    $before = Save-Snapshot 'BEFORE'
    $e1 = Get-EquipmentId 'EQ-001'
    $e2 = Get-EquipmentId 'EQ-002'

    $created = Invoke-Api POST '/api/v1/equipment' 201 'API-08 create EQ-008 (description null)' (Json @{ equipmentCode = 'EQ-008'; name = 'Webcam'; description = $null })
    $new = ConvertFrom-Json $created.Text
    Assert-Equal 'Location points at API-07' $created.Location "$BaseUrl/api/v1/equipment/$($new.id)"
    Assert-Equal 'new item is available' $new.isAvailable 'True'
    $null = Invoke-Api GET "/api/v1/equipment/$($new.id)" 200 'API-07 follow Location'
    $null = Invoke-Api PUT "/api/v1/equipment/$($new.id)" 200 'API-09 edit name' (Json @{ equipmentCode = 'EQ-008'; name = 'HD Webcam'; description = $null })

    $null = Invoke-Api PUT "/api/v1/equipment/$e1" 200 'API-09 descriptive change on E1' (Json @{ equipmentCode = 'EQ-001'; name = 'Camera'; description = 'Tripod-mount camera' })
    $null = Invoke-Api PUT "/api/v1/equipment/$e2" 400 'blank name' (Json @{ equipmentCode = 'EQ-002'; name = ' '; description = $null })
    $null = Invoke-Api POST '/api/v1/equipment' 409 'duplicate " eq-001 " (trim + case-insensitive)' (Json @{ equipmentCode = ' eq-001 '; name = 'Copy'; description = $null })
    $null = Invoke-Api PUT "/api/v1/equipment/$e1" 400 'isAvailable in update is a protected field' '{"equipmentCode":"EQ-001","name":"Camera","isAvailable":true}'

    $r = Invoke-Api GET "/api/v1/equipment/$e1" 200 'E1 is still unavailable'
    Assert-Equal 'E1 isAvailable' (ConvertFrom-Json $r.Text).isAvailable 'False'
    Test-CodeList 'E1 history is intact' "/api/v1/equipment/$e1/borrowings" 'EQ-001'

    $after = Save-Snapshot 'AFTER'
    Save-LastSnapshot $after
    Add-Expected 'one new available item (EQ-008, renamed HD Webcam); E1 has the new description, is still unavailable and keeps its active borrowing; no rows for the rejected requests; consistency check shows 0 rows.'
}

# ================================================================ T04

function Test-T04 {
    Reset-Data 'T04 - single issue and return (API clock 2026-11-10)'
    $before = Save-Snapshot 'BEFORE'
    $s2 = Get-StudentId 'ST-200'
    $s4 = Get-StudentId 'ST-400'
    $e1 = Get-EquipmentId 'EQ-001'
    $e2 = Get-EquipmentId 'EQ-002'
    $e6 = Get-EquipmentId 'EQ-006'

    $issued = Invoke-Api POST '/api/v1/borrowings' 201 'API-10 issue E2 to S4' (Json @{ studentId = $s4; equipmentId = $e2; expectedReturnDate = $script:Due })
    $borrowing = ConvertFrom-Json $issued.Text
    Assert-Equal 'Location points at API-13' $issued.Location "$BaseUrl/api/v1/borrowings/$($borrowing.id)"
    $r = Invoke-Api GET "/api/v1/borrowings/$($borrowing.id)" 200 'API-13 get the new borrowing'
    Assert-Text 'status is Active, return date null' $r.Text '"returnedDate":null'
    $afterIssue = Save-Snapshot 'AFTER ISSUE'

    $null = Invoke-Api POST '/api/v1/borrowings' 409 'ineligible student S2' (Json @{ studentId = $s2; equipmentId = $e6; expectedReturnDate = $script:Due })
    $null = Invoke-Api POST '/api/v1/borrowings' 409 'unavailable item E1' (Json @{ studentId = $s4; equipmentId = $e1; expectedReturnDate = $script:Due })
    $null = Invoke-Api POST '/api/v1/borrowings' 404 'missing student id' (Json @{ studentId = 99999; equipmentId = $e6; expectedReturnDate = $script:Due })
    $null = Invoke-Api POST '/api/v1/borrowings' 404 'missing equipment id' (Json @{ studentId = $s4; equipmentId = 99999; expectedReturnDate = $script:Due })
    $null = Invoke-Api POST '/api/v1/borrowings' 400 'date not later than the local date' (Json @{ studentId = $s4; equipmentId = $e6; expectedReturnDate = $script:Today })
    $null = Invoke-Api POST '/api/v1/borrowings' 400 'malformed date' (Json @{ studentId = $s4; equipmentId = $e6; expectedReturnDate = '2026-13-45' })
    $afterRejected = Save-Snapshot 'AFTER REJECTED ATTEMPTS'
    Assert-Same 'rejected attempts wrote nothing' $afterIssue $afterRejected

    $null = Invoke-Api POST "/api/v1/borrowings/$($borrowing.id)/return" 200 'API-12 return the borrowing' '{}'
    $r = Invoke-Api GET "/api/v1/borrowings/$($borrowing.id)" 200 'API-13 get after return'
    Assert-Text 'status Returned with the fixed clock date' $r.Text '"returnedDate":"2026-11-10"'
    $null = Invoke-Api POST "/api/v1/borrowings/$($borrowing.id)/return" 409 'repeat return'

    $after = Save-Snapshot 'AFTER RETURN'
    Save-LastSnapshot $after
    Add-Expected 'one borrowing for S4/E2 that is Returned with DateReturned 2026-11-10; E2 available again; rejected attempts added nothing; consistency check shows 0 rows. Now restart the API and run -Test AfterRestart.'
}

# ================================================================ T05

function Test-T05 {
    # 1. multi-item success
    Reset-Data 'T05 step 1 - checkout S4 with E2 + E6'
    $s4 = Get-StudentId 'ST-400'
    $e2 = Get-EquipmentId 'EQ-002'
    $e6 = Get-EquipmentId 'EQ-006'
    $null = Save-Snapshot 'BEFORE'
    $r = Invoke-Api POST '/api/v1/checkout-requests' 201 'API-11 checkout E2 + E6' (Json @{ studentId = $s4; equipmentIds = @($e2, $e6); expectedReturnDate = $script:Due })
    Assert-Equal 'one borrowing per item' @((ConvertFrom-Json $r.Text).borrowings).Count 2
    $null = Save-Snapshot 'AFTER'
    Add-Expected 'two new Active borrowings for S4 (EQ-002, EQ-006); both items unavailable.'

    # 2a. limit boundary through API-10
    Reset-Data 'T05 step 2a - S3 (two active) + E2 through API-10'
    $s3 = Get-StudentId 'ST-300'
    $e2 = Get-EquipmentId 'EQ-002'
    $null = Save-Snapshot 'BEFORE'
    $r = Invoke-Api POST '/api/v1/borrowings' 201 'API-10 third item for S3' (Json @{ studentId = $s3; equipmentId = $e2; expectedReturnDate = $script:Due })
    $null = Save-Snapshot 'AFTER'
    Add-Expected 'S3 has exactly three Active borrowings (EQ-004, EQ-005, EQ-002); E2 unavailable.'

    # 2b. limit boundary through API-11
    Reset-Data 'T05 step 2b - S1 (one active) + E2 + E6 through API-11'
    $s1 = Get-StudentId 'ST-100'
    $e2 = Get-EquipmentId 'EQ-002'
    $e6 = Get-EquipmentId 'EQ-006'
    $null = Save-Snapshot 'BEFORE'
    $r = Invoke-Api POST '/api/v1/checkout-requests' 201 'API-11 two more items for S1' (Json @{ studentId = $s1; equipmentIds = @($e2, $e6); expectedReturnDate = $script:Due })
    Assert-Equal 'two borrowings returned' @((ConvertFrom-Json $r.Text).borrowings).Count 2
    $null = Save-Snapshot 'AFTER'
    Add-Expected 'S1 has exactly three Active borrowings (EQ-001, EQ-002, EQ-006); E2 and E6 unavailable.'

    # 2c. over the limit
    Reset-Data 'T05 step 2c - S3 (two active) + E2 + E6 is over the limit'
    $s3 = Get-StudentId 'ST-300'
    $e2 = Get-EquipmentId 'EQ-002'
    $e6 = Get-EquipmentId 'EQ-006'
    $before = Save-Snapshot 'BEFORE'
    $null = Invoke-Api POST '/api/v1/checkout-requests' 409 'API-11 over the limit' (Json @{ studentId = $s3; equipmentIds = @($e2, $e6); expectedReturnDate = $script:Due })
    $after = Save-Snapshot 'AFTER'
    Assert-Same 'over-limit request left no partial rows' $before $after

    # 3. invalid requests, one at a time
    Reset-Data 'T05 step 3 - invalid requests'
    $s2 = Get-StudentId 'ST-200'
    $s4 = Get-StudentId 'ST-400'
    $e1 = Get-EquipmentId 'EQ-001'
    $e2 = Get-EquipmentId 'EQ-002'
    $e6 = Get-EquipmentId 'EQ-006'
    $before = Save-Snapshot 'BEFORE'
    $null = Invoke-Api POST '/api/v1/checkout-requests' 400 'duplicate ids' (Json @{ studentId = $s4; equipmentIds = @($e2, $e2); expectedReturnDate = $script:Due })
    $null = Invoke-Api POST '/api/v1/checkout-requests' 400 'only one item' (Json @{ studentId = $s4; equipmentIds = @($e2); expectedReturnDate = $script:Due })
    $null = Invoke-Api POST '/api/v1/checkout-requests' 409 'one unavailable item (E1 + E2)' (Json @{ studentId = $s4; equipmentIds = @($e1, $e2); expectedReturnDate = $script:Due })
    $null = Invoke-Api POST '/api/v1/checkout-requests' 409 'ineligible student S2' (Json @{ studentId = $s2; equipmentIds = @($e2, $e6); expectedReturnDate = $script:Due })
    $null = Invoke-Api POST '/api/v1/checkout-requests' 404 'missing equipment id' (Json @{ studentId = $s4; equipmentIds = @($e2, 99999); expectedReturnDate = $script:Due })
    $null = Invoke-Api POST '/api/v1/checkout-requests' 400 'invalid date' (Json @{ studentId = $s4; equipmentIds = @($e2, $e6); expectedReturnDate = $script:Today })
    $after = Save-Snapshot 'AFTER'
    Save-LastSnapshot $after
    Assert-Same 'six invalid requests wrote nothing' $before $after
    Add-Log ''
    Add-Log 'Step 4 (injected failure) is a separate run: -Test T05-fault.'
}

function Test-T05Fault {
    Reset-Data 'T05 step 4 - injected failure inside a two-item transaction'
    Add-Log 'Precondition: the API was restarted with EQUIPMENT_BORROWING_FAULT=BeforeCommit (the seam fails AFTER both writes were sent, BEFORE the commit). If it was not, this request succeeds and the test FAILS.'
    $s4 = Get-StudentId 'ST-400'
    $e2 = Get-EquipmentId 'EQ-002'
    $e6 = Get-EquipmentId 'EQ-006'
    $before = Save-Snapshot 'BEFORE'
    $r = Invoke-Api POST '/api/v1/checkout-requests' 500 'API-11 with injected failure' (Json @{ studentId = $s4; equipmentIds = @($e2, $e6); expectedReturnDate = $script:Due })
    Assert-Text 'safe error body' $r.Text '"code":"database_error"'
    $r = Invoke-Api GET "/api/v1/equipment/$e2" 200 'E2 after the failure'
    Assert-Equal 'E2 still available' (ConvertFrom-Json $r.Text).isAvailable 'True'
    $after = Save-Snapshot 'AFTER'
    Save-LastSnapshot $after
    Assert-Same 'rolled back: no partial rows, no availability change' $before $after
    Add-Log 'Documented failure point: after the INSERT/UPDATE statements for BOTH items were sent inside the transaction, before COMMIT (EfUnitOfWorkTransaction.CommitAsync -> FaultInjection.TriggerBeforeCommit).'
}

# ================================================================ T06

function Test-T06 {
    Reset-Data 'T06 - histories, search, filters, sorting'
    $before = Save-Snapshot 'BEFORE'
    $s1 = Get-StudentId 'ST-100'
    $s4 = Get-StudentId 'ST-400'
    $e1 = Get-EquipmentId 'EQ-001'
    $e3 = Get-EquipmentId 'EQ-003'

    Test-CodeList 'S1 history' "/api/v1/students/$s1/borrowings" 'EQ-001,EQ-003' $true
    $r = Invoke-Api GET "/api/v1/students/$s4/borrowings" 200 'S4 history (empty)'
    Assert-Equal 'empty history body' $r.Text.Trim() '[]'
    Test-CodeList 'E1 history' "/api/v1/equipment/$e1/borrowings" 'EQ-001'
    Test-CodeList 'E3 history (returned)' "/api/v1/equipment/$e3/borrowings" 'EQ-003'
    $null = Invoke-Api GET '/api/v1/students/99999/borrowings' 404 'unknown student'
    $null = Invoke-Api GET '/api/v1/equipment/99999/borrowings' 404 'unknown equipment'

    Test-CodeList 'search partial mixed-case name "aDa"' '/api/v1/borrowings?search=aDa' 'EQ-001,EQ-003'
    Test-CodeList 'search student number "st-300"' '/api/v1/borrowings?search=st-300' 'EQ-004,EQ-005'
    Test-CodeList 'search equipment name "PROJEC"' '/api/v1/borrowings?search=PROJEC' 'EQ-003'

    Test-CodeList 'status=Active' '/api/v1/borrowings?status=Active' 'EQ-001,EQ-004,EQ-005'
    Test-CodeList 'status=Returned' '/api/v1/borrowings?status=Returned' 'EQ-003'
    Test-CodeList 'status=All' '/api/v1/borrowings?status=All' 'EQ-001,EQ-003,EQ-004,EQ-005'

    Test-CodeList 'sort borrowedDate asc' '/api/v1/borrowings?sort=borrowedDate&direction=asc' 'EQ-001,EQ-003,EQ-004,EQ-005'
    Test-CodeList 'sort borrowedDate desc' '/api/v1/borrowings?sort=borrowedDate&direction=desc' 'EQ-005,EQ-004,EQ-003,EQ-001'
    Test-CodeList 'sort expectedReturnDate asc' '/api/v1/borrowings?sort=expectedReturnDate&direction=asc' 'EQ-001,EQ-003,EQ-004,EQ-005'
    Test-CodeList 'sort expectedReturnDate desc' '/api/v1/borrowings?sort=expectedReturnDate&direction=desc' 'EQ-005,EQ-004,EQ-003,EQ-001'

    Test-CodeList 'combined: Active + "cara"' '/api/v1/borrowings?status=Active&search=cara' 'EQ-004,EQ-005'
    Test-CodeList 'combined: Returned + "cara" (empty)' '/api/v1/borrowings?status=Returned&search=cara' ''
    Test-CodeList 'clear criteria = default (All, borrowedDate, asc)' '/api/v1/borrowings' 'EQ-001,EQ-003,EQ-004,EQ-005'
    Test-CodeList 'no matches' '/api/v1/borrowings?search=zzzz' ''

    $null = Invoke-Api GET '/api/v1/borrowings?status=Lost' 400 'unsupported status'
    $null = Invoke-Api GET '/api/v1/borrowings?sort=name' 400 'unsupported sort'
    $null = Invoke-Api GET '/api/v1/borrowings?direction=up' 400 'unsupported direction'

    $after = Save-Snapshot 'AFTER'
    Assert-Same 'T06 made no database changes' $before $after
    Add-Log ''
    Add-Log 'Note: equal dates (EQ-004/EQ-005, EQ-003/EQ-004/EQ-005) are ordered by borrowing id in the SAME direction as the sort. That is the documented choice for "stable borrowing ID breaks date ties".'
}

# ================================================================ AfterRestart

function Test-AfterRestart {
    Add-Log ''
    Add-Log '## Persistence after an API restart (no reset)'
    Add-Log 'Run this right after a mutation test and an API restart. It compares the database now with the final snapshot saved by that test.'
    $null = Invoke-Api GET '/api/v1/students' 200 'students after restart'
    $null = Invoke-Api GET '/api/v1/equipment' 200 'equipment after restart'
    $null = Invoke-Api GET '/api/v1/borrowings' 200 'borrowings after restart'
    $now = Save-Snapshot 'AFTER RESTART'
    if (Test-Path $script:LastSnapshotFile) {
        $saved = (Get-Content -Path $script:LastSnapshotFile -Raw).TrimEnd()
        Assert-Same 'committed data survived the restart' $saved $now.TrimEnd()
    }
    else {
        Add-Log 'No saved snapshot found (run T02, T03, T04, T05 or T05-fault first). Compare by eye instead.'
    }
}

# ================================================================ main

New-Item -ItemType Directory -Force -Path $script:EvidenceDir | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
Add-Log "# $Test - acceptance run"
Add-Log "- Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
Add-Log "- API: $BaseUrl"
Add-Log '- API clock should be 2026-11-10 (http-test-clock profile; check the API console for the TEST CLOCK ACTIVE line)'
Add-Log '- Database: equipment_borrowing_test (connection details are not logged)'

try {
    switch ($Test) {
        'T01' { Test-T01 }
        'T02' { Test-T02 }
        'T03' { Test-T03 }
        'T04' { Test-T04 }
        'T05' { Test-T05 }
        'T05-fault' { Test-T05Fault }
        'T06' { Test-T06 }
        'AfterRestart' { Test-AfterRestart }
    }
}
catch {
    $script:Fail++
    Add-Log ''
    Add-Log "**SCRIPT STOPPED:** $($_.Exception.Message)"
    Write-Host "SCRIPT STOPPED: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    Add-Log ''
    Add-Log "## Summary: $($script:Pass) passed, $($script:Fail) failed"
    Add-Log 'HTTP and unchanged-state checks above are automatic. Database expectations marked "Expected PostgreSQL state" must be checked by you against the snapshots.'
    $file = Join-Path $script:EvidenceDir "$Test-$stamp.md"
    Set-Content -Path $file -Value $script:Log -Encoding UTF8
    Write-Host ''
    Write-Host "Evidence written to $file  ($($script:Pass) passed, $($script:Fail) failed)"
}

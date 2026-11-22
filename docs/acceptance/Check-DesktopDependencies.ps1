# T07 architecture evidence: the desktop must not reach PostgreSQL, EF Core or Infrastructure.
# Run from the repository root AFTER building:  dotnet build
# Save the output:  .\docs\acceptance\Check-DesktopDependencies.ps1 | Tee-Object docs\acceptance\evidence\T07-dependencies.txt

$project = "src/EquipmentBorrowing.Desktop/EquipmentBorrowing.Desktop.csproj"
$folder  = Split-Path $project

"Checked: $(Get-Date -Format o)"
""
"== 1. Project references (expect: none) =="
dotnet list $project reference

""
"== 2. Direct + transitive packages matching database libraries (expect: none) =="
$hits = dotnet list $project package --include-transitive | Select-String -Pattern 'Npgsql|EntityFramework|Microsoft\.Data\.|Sqlite'
if ($hits) { $hits } else { "none" }

""
"== 3. Source files mentioning persistence terms (expect: none) =="
$files = Get-ChildItem $folder -Recurse -Include *.cs,*.axaml,*.csproj,*.json |
         Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
$terms = $files | Select-String -Pattern 'DbContext|Npgsql|EntityFramework|\.Infrastructure|\.Application|\.Domain|ConnectionString|SELECT\s|INSERT\s|UPDATE\s'
if ($terms) { $terms | ForEach-Object { "{0}:{1}: {2}" -f $_.Path, $_.LineNumber, $_.Line.Trim() } } else { "none" }

""
"== 4. Built output containing database assemblies (expect: none) =="
$dlls = Get-ChildItem "$folder/bin" -Recurse -Include *Npgsql*.dll,*EntityFramework*.dll,*EquipmentBorrowing.Infrastructure*.dll -ErrorAction SilentlyContinue
if ($dlls) { $dlls.FullName } else { "none" }

""
"== 5. HTTP usage (expect: HttpClient in Api/EquipmentApiClient.cs) =="
Get-ChildItem $folder -Recurse -Include *.cs | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    Select-String -Pattern 'HttpClient' | ForEach-Object { "{0}:{1}" -f $_.Path, $_.LineNumber }

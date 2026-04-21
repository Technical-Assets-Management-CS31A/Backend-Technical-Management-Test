# Generate-TestReport.ps1
# Runs dotnet test, captures TRX output, and produces a 3-level hierarchical HTML report:
#   Folder (Services / Controllers / Utilities / Infrastructure)
#     +-- Class (AuthServiceTests, ItemControllerTests, ...)
#           +-- Test (individual [Fact] / [Theory] cases)
#
# Usage (from repo root):
#   powershell -ExecutionPolicy Bypass -File BackendTechincalAssetsManagementTest\Generate-TestReport.ps1
# Output: BackendTechincalAssetsManagementTest\TestResults\TestReport.html

$projectPath = "$PSScriptRoot\BackendTechincalAssetsManagementTest.csproj"
$resultsDir  = "$PSScriptRoot\TestResults"
$trxPath     = "$resultsDir\results.trx"
$htmlPath    = "$resultsDir\TestReport.html"

# -- 1. Run tests ------------------------------------------------------------------
Write-Host "Running tests..." -ForegroundColor Cyan
dotnet test $projectPath --logger "trx;LogFileName=results.trx" --results-directory $resultsDir | Out-Null

if (-not (Test-Path $trxPath)) {
    Write-Error "TRX file not found at $trxPath"
    exit 1
}

# -- 2. Parse TRX ------------------------------------------------------------------
[xml]$trx   = Get-Content $trxPath -Encoding UTF8
$counters   = $trx.TestRun.ResultSummary.Counters
$total      = [int]$counters.total
$passed     = [int]$counters.passed
$failed     = [int]$counters.failed
$outcome    = $trx.TestRun.ResultSummary.outcome
$startTime  = $trx.TestRun.Times.start
$finishTime = $trx.TestRun.Times.finish

# result lookup: testId → UnitTestResult
$results = @{}
foreach ($r in $trx.TestRun.Results.UnitTestResult) { $results[$r.testId] = $r }

# -- 3. Build 3-level structure: folder -> class -> []test ------------------------
# Namespace pattern: BackendTechincalAssetsManagementTest.<Folder>.<ClassName>
# e.g. BackendTechincalAssetsManagementTest.Controllers.AuthControllerTests
#      BackendTechincalAssetsManagementTest.Services.AuthServiceTests
#      BackendTechincalAssetsManagementTest.Utilities.PasswordHashingServiceTests
#      BackendTechincalAssetsManagementTest.Infrastructure.GlobalExceptionHandlerTests

$tree = @{}   # folder → { className → []test }

foreach ($td in $trx.TestRun.TestDefinitions.UnitTest) {
    $fullClass = $td.TestMethod.className   # full namespace + class
    $parts     = $fullClass -split '\.'

    # folder = second-to-last segment if the namespace has 3+ parts, else "Other"
    # e.g. BackendTechincalAssetsManagementTest.Controllers.AuthControllerTests → Controllers
    #      BackendTechincalAssetsManagementTest.UnitTest1 (2 parts)             → Other
    $folder    = if ($parts.Count -ge 3) { $parts[-2] } else { "Other" }
    $className = $parts[-1]

    $result = $results[$td.id]

    $testEntry = @{
        Name     = $td.name
        Outcome  = if ($result) { $result.outcome } else { "NotRun" }
        Duration = if ($result) { $result.duration } else { "" }
        Message  = if ($result -and $result.Output -and $result.Output.ErrorInfo -and $result.Output.ErrorInfo.Message) {
                       $result.Output.ErrorInfo.Message.'#text'
                   } else { "" }
    }

    if (-not $tree.ContainsKey($folder))              { $tree[$folder] = @{} }
    if (-not $tree[$folder].ContainsKey($className))  { $tree[$folder][$className] = @() }
    $tree[$folder][$className] += $testEntry
}

# -- 4. Helper functions -----------------------------------------------------------
function HtmlEncode($s) {
    if (-not $s) { return "" }
    $s -replace '&','&amp;' -replace '<','&lt;' -replace '>','&gt;' -replace '"','&quot;'
}

function DurationMs($dur) {
    if (-not $dur) { return "" }
    try { $ms = [int]([TimeSpan]::Parse($dur).TotalMilliseconds); return "${ms} ms" }
    catch { return $dur }
}

function SumMs($items) {
    ($items | ForEach-Object {
        if ($_.Duration) { try { [int]([TimeSpan]::Parse($_.Duration).TotalMilliseconds) } catch { 0 } } else { 0 }
    } | Measure-Object -Sum).Sum
}

function PassIcon($o) {
    switch ($o) {
        "Passed" { '<span class="icon pass">&#10004;</span>' }
        "Failed" { '<span class="icon fail">&#10006;</span>' }
        default  { '<span class="icon skip">&#8212;</span>' }
    }
}
function OC($o) { if ($o -eq "Passed") { "pass" } elseif ($o -eq "Failed") { "fail" } else { "skip" } }

# -- 5. Render HTML rows -----------------------------------------------------------
$rowsHtml    = ""
$folderIndex = 0

foreach ($folder in ($tree.Keys | Sort-Object)) {
    $classes = $tree[$folder]

    # Aggregate folder stats
    $allTests      = $classes.Values | ForEach-Object { $_ } | ForEach-Object { $_ }
    $folderTotal   = ($allTests | Measure-Object).Count
    $folderPassed  = ($allTests | Where-Object { $_.Outcome -eq "Passed" }  | Measure-Object).Count
    $folderFailed  = ($allTests | Where-Object { $_.Outcome -eq "Failed" }  | Measure-Object).Count
    $folderMs      = SumMs $allTests
    $folderOutcome = if ($folderFailed -gt 0) { "fail" } else { "pass" }
    $folderId      = "folder_${folderIndex}"
    $folderIndex++

    $rowsHtml += @"
<tr class="folder-row $folderOutcome" onclick="toggleFolder('$folderId')">
  <td class="expander folder-expander">
    $(PassIcon $folderOutcome)
    <span class="chevron"></span>
    <span class="folder-label">$folder</span>
    <span class="count">$folderPassed / $folderTotal passed</span>
  </td>
  <td class="dur">${folderMs} ms</td>
</tr>
<tbody id="$folderId" class="folder-body hidden">
"@

    $classIndex = 0
    foreach ($className in ($classes.Keys | Sort-Object)) {
        $tests       = $classes[$className]
        $classTotal  = ($tests | Measure-Object).Count
        $classPassed = ($tests | Where-Object { $_.Outcome -eq "Passed" } | Measure-Object).Count
        $classFailed = ($tests | Where-Object { $_.Outcome -eq "Failed" } | Measure-Object).Count
        $classMs     = SumMs $tests
        $classOutcome = if ($classFailed -gt 0) { "fail" } else { "pass" }
        $classId      = "class_${folderIndex}_${classIndex}"
        $classIndex++

        $rowsHtml += @"
  <tr class="class-row $classOutcome" onclick="toggleClass('$classId', event)">
    <td class="expander class-expander">
      $(PassIcon $classOutcome)
      <span class="chevron"></span>
      <strong>$className</strong>
      <span class="count">$classPassed / $classTotal</span>
    </td>
    <td class="dur">${classMs} ms</td>
  </tr>
  <tbody id="$classId" class="class-body hidden">
"@

        foreach ($t in ($tests | Sort-Object { $_.Name })) {
            $oc      = OC $t.Outcome
            $icon    = PassIcon $t.Outcome
            $dur     = DurationMs $t.Duration
            $name    = HtmlEncode $t.Name
            $msgHtml = ""
            if ($t.Message) {
                $msg     = HtmlEncode $t.Message
                $msgHtml = "<div class='error-msg'>$msg</div>"
            }
            $rowsHtml += @"
    <tr class="test-row $oc">
      <td class="test-name">$icon <span class="tname">$name</span>$msgHtml</td>
      <td class="dur">$dur</td>
    </tr>
"@
        }

        $rowsHtml += "  </tbody>`n"
    }

    $rowsHtml += "</tbody>`n"
}

# -- 6. Assemble full HTML ---------------------------------------------------------
$summaryClass = if ($failed -gt 0) { "fail" } else { "pass" }

$html = @"
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Test Report - BackendTechnicalAssetsManagement</title>
<style>
* { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: 'Segoe UI', Arial, sans-serif; font-size: 13px; background: #f0f2f5; color: #1a1a2e; }

/* -- Header -- */
header { background: #1e2a3a; color: #fff; padding: 20px 32px; }
header h1 { font-size: 20px; font-weight: 600; display: flex; align-items: center; gap: 10px; }
header p  { font-size: 12px; color: #8899aa; margin-top: 5px; }
.badge { padding: 3px 12px; border-radius: 20px; font-size: 12px; font-weight: 700; }
.badge.pass { background: #d1fae5; color: #065f46; }
.badge.fail { background: #fee2e2; color: #991b1b; }

/* -- Summary bar -- */
.summary { display: flex; gap: 14px; padding: 14px 32px; background: #fff; border-bottom: 1px solid #dde3ee; flex-wrap: wrap; }
.stat { background: #f5f7ff; border-radius: 8px; padding: 10px 22px; text-align: center; min-width: 88px; }
.stat .num { font-size: 28px; font-weight: 700; line-height: 1; }
.stat .lbl { font-size: 11px; color: #777; margin-top: 3px; }
.stat.total .num { color: #0550ae; }
.stat.pass  .num { color: #1a7f37; }
.stat.fail  .num { color: #cf222e; }

/* -- Table -- */
.container { padding: 22px 32px; }
table { width: 100%; border-collapse: collapse; background: #fff;
        border-radius: 10px; overflow: hidden; box-shadow: 0 1px 6px rgba(0,0,0,.09); }
thead th { background: #1e2a3a; color: #fff; padding: 10px 16px;
           font-size: 11px; font-weight: 600; letter-spacing: .05em; text-transform: uppercase; }
thead th:last-child { width: 90px; text-align: right; }

/* -- Folder rows (level 1) -- */
.folder-row td { padding: 11px 16px; background: #eef2ff;
                 border-top: 3px solid #c7d2fe; cursor: pointer; user-select: none; }
.folder-row:hover td { background: #e0e7ff; }
.folder-row.pass td { border-left: 5px solid #1a7f37; }
.folder-row.fail td { border-left: 5px solid #cf222e; }
.folder-label { font-size: 13px; font-weight: 700; letter-spacing: .02em; }

/* -- Class rows (level 2) -- */
.class-row td { padding: 8px 16px 8px 32px; background: #f8fafc;
                border-top: 1px solid #e2e8f0; cursor: pointer; user-select: none; }
.class-row:hover td { background: #f0f4ff; }
.class-row.pass td { border-left: 4px solid #86efac; }
.class-row.fail td { border-left: 4px solid #fca5a5; }

/* -- Test rows (level 3) -- */
.test-row td { padding: 6px 16px 6px 56px; border-top: 1px solid #f3f4f6; }
.test-row.pass td { background: #fff; }
.test-row.fail td { background: #fff8f8; }
.test-row:hover td { background: #f9fafb; }
.tname { color: #374151; }

/* -- Shared -- */
.dur { text-align: right; color: #9ca3af; font-size: 12px; white-space: nowrap; }
.count { font-size: 11px; color: #6b7280; font-weight: 400; margin-left: 8px; }
.expander { display: flex; align-items: center; gap: 6px; }

/* CSS-only chevron - zero Unicode, zero encoding issues */
.chevron {
  display: inline-block;
  width: 8px; height: 8px;
  border-right: 2px solid #9ca3af;
  border-bottom: 2px solid #9ca3af;
  transform: rotate(-45deg);   /* points right (collapsed) */
  transition: transform .15s ease;
  flex-shrink: 0;
  margin-right: 2px;
}
.open .chevron {
  transform: rotate(45deg);    /* points down (expanded) */
}

.icon { font-size: 12px; flex-shrink: 0; }
.icon.pass { color: #16a34a; }
.icon.fail { color: #dc2626; }
.icon.skip { color: #9ca3af; }
.test-name { display: flex; flex-direction: column; gap: 3px; }
.error-msg { font-size: 11px; color: #dc2626; background: #fef2f2;
             border-left: 3px solid #fca5a5; padding: 4px 8px; margin-top: 3px;
             border-radius: 3px; white-space: pre-wrap; word-break: break-word; }
.hidden { display: none; }

footer { text-align: center; padding: 18px; color: #9ca3af; font-size: 11px; }
</style>
</head>
<body>

<header>
  <h1>Test Report - BackendTechnicalAssetsManagement <span class="badge $summaryClass">$outcome</span></h1>
  <p>Started: $startTime &nbsp;&middot;&nbsp; Finished: $finishTime</p>
</header>

<div class="summary">
  <div class="stat total"><div class="num">$total</div><div class="lbl">Total</div></div>
  <div class="stat pass"><div class="num">$passed</div><div class="lbl">Passed</div></div>
  <div class="stat fail"><div class="num">$failed</div><div class="lbl">Failed</div></div>
</div>

<div class="container">
<table>
  <thead><tr><th>Folder / Class / Test</th><th>Duration</th></tr></thead>
  <tbody>
$rowsHtml
  </tbody>
</table>
</div>

<footer>Generated by Generate-TestReport.ps1 &nbsp;&middot;&nbsp; xUnit &middot; Moq &middot; FluentAssertions</footer>

<script>
function toggleFolder(id) {
  var body = document.getElementById(id);
  var row  = body.previousElementSibling;
  var open = !body.classList.contains('hidden');
  body.classList.toggle('hidden', open);
  row.classList.toggle('open', !open);
}
function toggleClass(id, e) {
  e.stopPropagation();
  var body = document.getElementById(id);
  var row  = body.previousElementSibling;
  var open = !body.classList.contains('hidden');
  body.classList.toggle('hidden', open);
  row.classList.toggle('open', !open);
}
// On load: expand all folders; auto-expand failed classes
document.addEventListener('DOMContentLoaded', function () {
  document.querySelectorAll('.folder-row').forEach(function (row) {
    var id = row.getAttribute('onclick').match(/'([^']+)'/)[1];
    document.getElementById(id).classList.remove('hidden');
    row.classList.add('open');
  });
  document.querySelectorAll('.class-row.fail').forEach(function (row) {
    var id = row.getAttribute('onclick').match(/'([^']+)'/)[1];
    document.getElementById(id).classList.remove('hidden');
    row.classList.add('open');
  });
});
</script>
</body>
</html>
"@

# -- 7. Write & open ---------------------------------------------------------------
# Write with plain UTF-8 (no BOM) so no stray bytes appear in the output
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($htmlPath, $html, $utf8NoBom)

Write-Host ""
Write-Host "Report generated: $htmlPath" -ForegroundColor Green
Write-Host "Total: $total  |  Passed: $passed  |  Failed: $failed" `
    -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Green" })

Start-Process $htmlPath

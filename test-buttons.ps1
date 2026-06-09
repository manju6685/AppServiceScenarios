<#
.SYNOPSIS
    Drives every diagnostic button on Default.aspx. Designed to be watched in
    Application Insights -> Live Metrics while it runs.

.PARAMETER SiteName
    App Service site name (without the azurewebsites.net suffix).
    Example: -SiteName appservicescenarios

.PARAMETER BaseUrl
    Full base URL override (e.g. https://myapp.azurewebsites.net or a slot URL).
    If supplied, overrides -SiteName.

.PARAMETER Loop
    Keep cycling through the safe buttons forever (Ctrl+C to stop). Best mode for
    Live Metrics — produces a steady stream of requests + traces + exceptions.

.PARAMETER DurationMinutes
    With -Loop, stop after N minutes instead of running forever.

.PARAMETER DelayMs
    Delay between button invocations (ms). Default 500.

.PARAMETER IncludeHeavy
    Include the long-running / resource-pressure buttons (Slow60, Slow60x100,
    MemoryLeak, SocketExhaust, MixedBurst, RunLoadTest, OutboundHang,
    LargePayload, ParallelReal, etc.). ON by default in single-shot mode,
    OFF by default in -Loop mode (they block the worker and kill Live Metrics
    throughput).

.PARAMETER IncludeDestructive
    Also include process-killing buttons (Deadlock, ThreadPoolStarve, BgCrash,
    FailFast). OFF by default — recycling the worker breaks the Live Metrics view.

.PARAMETER Only
    Run only the buttons whose names match these wildcards. Example:
        -Only Button500*,*Slow*
    Useful for demoing a single failure mode.

.EXAMPLE
    .\test-buttons.ps1 -SiteName appservicescenarios -Loop -DurationMinutes 10

.EXAMPLE
    .\test-buttons.ps1 -SiteName appservicescenarios -Only ButtonHttp500ParallelReal
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SiteName,

    [string]$BaseUrl,

    [switch]$Loop,
    [int]$DurationMinutes = 0,
    [int]$DelayMs = 500,
    [switch]$IncludeDestructive,
    [Nullable[bool]]$IncludeHeavy = $null,
    [string[]]$Only
)

# --- Resolve target URL -----------------------------------------------------
if (-not $BaseUrl) {
    if (-not $SiteName) {
        $SiteName = Read-Host "Enter App Service site name (e.g. appservicescenarios)"
    }
    $BaseUrl = "https://$SiteName.azurewebsites.net"
}
$BaseUrl = $BaseUrl.TrimEnd('/')
Write-Host "Target: $BaseUrl/Default.aspx" -ForegroundColor Cyan
Write-Host "Open Live Metrics now: https://portal.azure.com/#blade/AppInsightsExtension/QuickPulseBladeV2" -ForegroundColor Yellow
Write-Host ""

# --- Helper: post to Default.aspx with VIEWSTATE/EVENTTARGET to invoke a button.
function Invoke-Button {
    param(
        [string]$ButtonName,
        [int]$TimeoutSec = 30,
        [hashtable]$ExtraFields = @{}
    )
    $ProgressPreference = 'SilentlyContinue'
    [System.Net.ServicePointManager]::SecurityProtocol = 'Tls12'
    try {
        $page = Invoke-WebRequest -Uri "$BaseUrl/Default.aspx" -UseBasicParsing -SessionVariable s -TimeoutSec $TimeoutSec -ErrorAction Stop
    } catch {
        return [pscustomobject]@{ Button = $ButtonName; Status = 0; Length = 0; Error = "GET failed: $($_.Exception.Message)" }
    }
    $vs  = ([regex]::Match($page.Content, '<input[^>]*name="__VIEWSTATE"[^>]*value="([^"]*)"')).Groups[1].Value
    $vsg = ([regex]::Match($page.Content, '<input[^>]*name="__VIEWSTATEGENERATOR"[^>]*value="([^"]*)"')).Groups[1].Value
    $ev  = ([regex]::Match($page.Content, '<input[^>]*name="__EVENTVALIDATION"[^>]*value="([^"]*)"')).Groups[1].Value
    $body = @{
        '__VIEWSTATE'          = $vs
        '__VIEWSTATEGENERATOR' = $vsg
        '__EVENTVALIDATION'    = $ev
        $ButtonName            = 'Execute Test'
    }
    foreach ($k in $ExtraFields.Keys) { $body[$k] = $ExtraFields[$k] }
    try {
        $r = Invoke-WebRequest -Uri "$BaseUrl/Default.aspx" -Method Post -Body $body -UseBasicParsing -WebSession $s -TimeoutSec $TimeoutSec -ErrorAction Stop
        return [pscustomobject]@{ Button = $ButtonName; Status = [int]$r.StatusCode; Length = $r.Content.Length }
    } catch {
        $resp = $_.Exception.Response
        return [pscustomobject]@{ Button = $ButtonName; Status = if ($resp) { [int]$resp.StatusCode } else { 0 }; Length = 0; Error = $_.Exception.Message }
    }
}

# --- Button catalog ---------------------------------------------------------
# FAST — return quickly, safe for tight looping in Live Metrics.
$fastButtons = @(
    # Status-code generators (Failed Requests stream)
    'Button400','Button401','Button403','Button404','Button408','Button429',
    'Button502','Button503','Button504',
    'Button1','Button2','Button3',           # legacy variants (some return 5xx)
    'Button4',                               # 100x HTTP 500 (server-side burst, fast return)
    'ButtonThrottled',                       # 429 with Retry-After
    'ButtonMissingAppSetting',

    # Single slow request (real 10s, but only one)
    'ButtonSlow10',

    # Fast dependency / identity / health pings
    'ButtonDNSFail',
    'ButtonRedisTimeout',
    'ButtonSqlSlowQuery',
    'ButtonInstanceIdentity',
    'ButtonAvailabilityTestEndpoint',
    'ButtonHealthCheckFailing',
    'ButtonLOH'
)

# HEAVY — long-running, fire-and-forget, or resource pressure. Will block
# subsequent page GETs if looped tightly. Use for one-shot demos.
$heavyButtons = @(
    'ButtonSlow60',
    'ButtonSlow10x100',
    'ButtonSlow60x100',
    'ButtonSlow10ParallelReal',              # 100 real parallel GETs
    'ButtonHttp500ParallelReal',             # 100 real parallel GETs -> auto-heal
    'ButtonMixedBurst',
    'ButtonCustomEventBurst',
    'ButtonRunLoadTest',                     # configurable load test
    'ButtonOutboundHang',                    # 30s outbound to unreachable
    'ButtonSqlConnectionPoolExhaust',
    'ButtonTls10Only',
    'ButtonLargePayload',                    # ~10 MB stream
    'ButtonDiskIO',                          # 500 MB disk write
    'ButtonMemoryLeak',                      # 100 MB to static
    'ButtonSocketExhaust'                    # HttpClient anti-pattern
)

# DESTRUCTIVE — recycle the worker. Breaks Live Metrics for ~30s each.
$destructiveButtons = @(
    'ButtonThreadPoolStarve',
    'ButtonDeadlock',
    'ButtonBgCrash',
    'ButtonFailFast'
)

# Decide which sets to include.
# - Loop mode: heavy OFF by default (steady stream for Live Metrics).
# - Single-shot mode: heavy ON by default (cover all buttons once).
# - User can override via -IncludeHeavy / -IncludeHeavy:$false.
if ($IncludeHeavy -eq $null) {
    $useHeavy = -not $Loop
} else {
    $useHeavy = [bool]$IncludeHeavy
}

$buttons = @($fastButtons)
if ($useHeavy)         { $buttons += $heavyButtons }
if ($IncludeDestructive) { $buttons += $destructiveButtons }
if ($Only) {
    $buttons = $buttons | Where-Object { $name = $_; ($Only | Where-Object { $name -like $_ }).Count -gt 0 }
}

if (-not $buttons -or $buttons.Count -eq 0) {
    Write-Host "No buttons matched filter." -ForegroundColor Red
    return
}

$inc = @()
$inc += "fast=$($fastButtons.Count)"
if ($useHeavy)           { $inc += "heavy=$($heavyButtons.Count)" }
if ($IncludeDestructive) { $inc += "destructive=$($destructiveButtons.Count)" }
Write-Host ("Buttons: {0} total ({1})" -f $buttons.Count, ($inc -join ', ')) -ForegroundColor Cyan

# --- Execution --------------------------------------------------------------
$loadTestFields = @{
    'LoadTestCount'       = '50'
    'LoadTestConcurrency' = '10'
    'LoadTestPath'        = '/HealthCheck.aspx'
}

$started   = Get-Date
$iteration = 0
# Shorter per-call timeout in loop mode so a stuck button can't stall the iteration.
$perCallTimeout = if ($Loop) { 15 } else { 90 }
do {
    $iteration++
    Write-Host ("`n=== Iteration {0} @ {1:HH:mm:ss} ({2} buttons, {3}s timeout) ===" -f $iteration, (Get-Date), $buttons.Count, $perCallTimeout) -ForegroundColor Cyan
    foreach ($b in $buttons) {
        $extras = if ($b -eq 'ButtonRunLoadTest') { $loadTestFields } else { @{} }
        $r = Invoke-Button -ButtonName $b -TimeoutSec $perCallTimeout -ExtraFields $extras
        if ($r.Error) {
            Write-Host ("  {0,-32} -> ERR  {1}" -f $r.Button, $r.Error) -ForegroundColor Yellow
        } else {
            $color = if ($r.Status -ge 500) { 'Red' } elseif ($r.Status -ge 400) { 'Yellow' } else { 'Green' }
            Write-Host ("  {0,-32} -> HTTP {1,-3} len={2}" -f $r.Button, $r.Status, $r.Length) -ForegroundColor $color
        }
        Start-Sleep -Milliseconds $DelayMs
    }

    if (-not $Loop) { break }
    if ($DurationMinutes -gt 0 -and ((Get-Date) - $started).TotalMinutes -ge $DurationMinutes) {
        Write-Host "`nDuration reached ($DurationMinutes min). Stopping." -ForegroundColor Cyan
        break
    }
} while ($true)

Write-Host "`nDone. Total iterations: $iteration  Elapsed: $([int]((Get-Date) - $started).TotalSeconds)s" -ForegroundColor Cyan

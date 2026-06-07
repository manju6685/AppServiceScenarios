param([string]$BaseUrl = 'https://manjuAppServiceScenarios.azurewebsites.net')

# Helper: post to Default.aspx with VIEWSTATE/EVENTTARGET to invoke a specific button.
function Invoke-Button {
    param(
        [string]$ButtonName,
        [int]$TimeoutSec = 30
    )
    $ProgressPreference = 'SilentlyContinue'
    [System.Net.ServicePointManager]::SecurityProtocol = 'Tls12'
    try {
        $page = Invoke-WebRequest -Uri "$BaseUrl/Default.aspx" -UseBasicParsing -SessionVariable s -TimeoutSec $TimeoutSec -ErrorAction Stop
    } catch {
        Write-Host ("  GET failed: {0}" -f $_.Exception.Message) -ForegroundColor Red
        return $null
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
    try {
        $r = Invoke-WebRequest -Uri "$BaseUrl/Default.aspx" -Method Post -Body $body -UseBasicParsing -WebSession $s -TimeoutSec $TimeoutSec -ErrorAction Stop
        return [pscustomobject]@{ Button = $ButtonName; Status = [int]$r.StatusCode; Length = $r.Content.Length }
    } catch {
        $resp = $_.Exception.Response
        return [pscustomobject]@{ Button = $ButtonName; Status = if ($resp) { [int]$resp.StatusCode } else { 0 }; Length = 0; Error = $_.Exception.Message }
    }
}

$buttons = @(
    'Button4',            # 100 HTTP 500
    'ButtonSlow10x100',   # 100 slow 10s
    'ButtonSlow60x100',   # 100 slow 60s
    'Button400', 'Button401', 'Button403', 'Button404',
    'Button408', 'Button429', 'Button502', 'Button503', 'Button504',
    'ButtonMixedBurst',
    'ButtonSlow10',       # single slow 10s (real request)
    'ButtonOutboundHang', # 30s outbound to unreachable
    'ButtonLargePayload', # ~10 MB stream
    'ButtonLOH',          # LOH GC pressure
    'ButtonSocketExhaust',# bg HttpClient anti-pattern
    'ButtonMemoryLeak',   # 100 MB to static
    'ButtonDiskIO',       # 500 MB disk write
    'ButtonThreadPoolStarve',
    'ButtonDeadlock'
)

Write-Host "=== Triggering buttons (skip crash buttons until end) ===" -ForegroundColor Cyan
foreach ($b in $buttons) {
    $r = Invoke-Button -ButtonName $b -TimeoutSec 90
    if ($r) {
        Write-Host ("  {0,-25} -> HTTP {1,-3} len={2}" -f $r.Button, $r.Status, $r.Length)
    }
    Start-Sleep -Milliseconds 500
}

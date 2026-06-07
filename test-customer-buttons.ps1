$base = 'https://manjuAppServiceScenarios.azurewebsites.net'
$u = "$base/Default.aspx"
$sess = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$r0 = Invoke-WebRequest $u -WebSession $sess -UseBasicParsing -TimeoutSec 30
$html = $r0.Content
Write-Host ("Home GET -> HTTP {0}" -f $r0.StatusCode)

function Get-FormToken {
    param([string]$h, [string]$name)
    # Hidden fields render as: <input type="hidden" name="X" id="X" value="..." />
    $pat = "name=`"$name`"[^>]*?value=`"([^`"]*)`""
    $m = [regex]::Match($h, $pat)
    return $m.Groups[1].Value
}

$btns = @(
    'ButtonDNSFail',
    'ButtonSqlConnectionPoolExhaust',
    'ButtonSqlSlowQuery',
    'ButtonRedisTimeout',
    'ButtonInstanceIdentity',
    'ButtonCustomEventBurst',
    'ButtonAvailabilityTestEndpoint',
    'ButtonHealthCheckFailing',
    'ButtonTls10Only',
    'ButtonMissingAppSetting'
)

foreach ($b in $btns) {
    $vs  = Get-FormToken $html '__VIEWSTATE'
    $vsg = Get-FormToken $html '__VIEWSTATEGENERATOR'
    $ev  = Get-FormToken $html '__EVENTVALIDATION'
    $body = @{
        '__VIEWSTATE'          = $vs
        '__VIEWSTATEGENERATOR' = $vsg
        '__EVENTVALIDATION'    = $ev
        $b                     = 'Execute Test'
    }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $r = Invoke-WebRequest $u -WebSession $sess -UseBasicParsing -TimeoutSec 60 -Method POST -Body $body
        $sw.Stop()
        $html = $r.Content
        Write-Host ("{0,-40} -> HTTP {1}  ({2}ms)" -f $b, $r.StatusCode, $sw.ElapsedMilliseconds)
    }
    catch {
        $sw.Stop()
        Write-Host ("{0,-40} -> ERR {1}  ({2}ms)" -f $b, $_.Exception.Message, $sw.ElapsedMilliseconds)
    }
}
Write-Host "Done."

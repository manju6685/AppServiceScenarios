# AppServiceScenarios

ASP.NET Web Forms (.NET Framework 4.8.1) test app for **reproducing common Azure
App Service performance and reliability problems** and verifying that they are
captured by **Application Insights**.

A single page (`Default.aspx`) exposes 32 buttons. Each button triggers one
realistic failure mode (slow response, high CPU, memory leak, threadpool
starvation, deadlock, large payload, outbound hang, disk I/O, socket
exhaustion, LOH pressure, throttled response), generates a batch of synthetic
HTTP records (100 × 4xx, 100 × 5xx, 100 × slow, mixed bursts), or simulates
a real customer support pattern (DNS failure, SQL connection-pool exhaustion,
slow query, Redis timeout, TLS-mismatch, missing config, health-check
failures, custom-event burst, multi-region availability, instance identity).

It is intentionally **not safe for production** — it crashes the worker, fills
memory, and exhausts sockets on demand.

---

## Why this exists

Application Insights' **codeless agent (`ApplicationInsightsAgent_EXTENSION_VERSION = ~2`)**
on .NET Framework auto-captures inbound GET requests reliably, but does not
reliably capture:

- Postbacks (`POST /Default.aspx`) under sampling pressure
- Custom `Trace.*` output unless a TraceListener is wired up
- Work executed on `Task.Run` background threads

This project demonstrates how to bridge those gaps by emitting telemetry
explicitly with `TelemetryClient`, so every button produces a row in
Application Insights regardless of whether the agent saw it.

---

## Layout

| File | Purpose |
|---|---|
| `Default.aspx` / `Default.aspx.cs` | The 32-button scenario page |
| `test-customer-buttons.ps1` | Drives the 10 customer-scenario buttons |
| `Slow10.aspx` / `Slow60.aspx` | Standalone slow endpoints (real `Thread.Sleep`) |
| `Http500.aspx`, `FastResponse.aspx`, `HealthCheck.aspx` | Ancillary endpoints |
| `Web.config` | Wires the AI TraceListener so `Trace.*` flows to AI `traces` |
| `packages.config` | NuGet refs (`Microsoft.ApplicationInsights` 2.22.0, `.TraceListener` 2.22.0) |
| `test-buttons.ps1` | Drives every button via VIEWSTATE postbacks |

---

## Telemetry strategy

Two helpers in `Default.aspx.cs`:

### 1. `TrackInlineButton(buttonName)` — for single-page handlers

An `IDisposable` wrapper that records a `RequestTelemetry` named
`POST /Default.aspx [ButtonXxx]` with the actual elapsed `Stopwatch` time and
flushes when disposed. Wrapping the body in `using (TrackInlineButton(...))`
guarantees a row in AI even if the codeless agent misses the postback.

```csharp
protected void ButtonSlow10_Click(object sender, EventArgs e)
{
    using (TrackInlineButton("ButtonSlow10"))
    {
        Thread.Sleep(10000);
        // ...
    }
}
```

### 2. `EmitSyntheticBatch(page, statusCode, count, durationMs, batchTag)` — for "100×" batch buttons

Spawns a `Task.Run` that emits `count` `RequestTelemetry` records (and matching
`ExceptionTelemetry` for non-2xx/3xx). Each row carries `synthetic=true` and
`batch=<tag>` custom dimensions so they're easy to filter out of real traffic.

No outbound HTTP is made — the records are written straight to AI's ingestion
endpoint via `TelemetryClient.TrackRequest`.

### TraceListener (`Web.config`)

```xml
<system.diagnostics>
  <trace autoflush="true">
    <listeners>
      <add name="appInsightsListener"
           type="Microsoft.ApplicationInsights.TraceListener.ApplicationInsightsTraceListener,
                 Microsoft.ApplicationInsights.TraceListener" />
    </listeners>
  </trace>
</system.diagnostics>
```

Routes `Trace.TraceInformation` / `Trace.TraceError` to the AI `traces` table.

---

## The 32 buttons

### Real single-page postbacks (instrumented with `TrackInlineButton`)

| Button | Behaviour | Expected duration |
|---|---|---|
| `ButtonSlow10` | `Thread.Sleep(10s)` | 10 s |
| `ButtonSlow60` | `Thread.Sleep(60s)` | 60 s |
| `ButtonThreadPoolStarve` | Queues 200 × 60s blocking work items | < 1 s (return), starves for 60 s |
| `ButtonDeadlock` | Sync-over-async deadlock on a background task | < 1 s (return) |
| `ButtonMemoryLeak` | +100 MB to a static `List<byte[]>` per click | ~1 s |
| `ButtonLargePayload` | Streams 10 MB unbuffered | 1–2 s |
| `ButtonOutboundHang` | `WebRequest` to `192.0.2.1` (RFC5737), 30 s timeout | ~21 s |
| `ButtonDiskIO` | Writes/deletes 10 × 50 MB files in `%TEMP%` | 2–5 s |
| `ButtonSocketExhaust` | 200 short-lived `HttpClient` instances → SNAT pressure | < 1 s (return) |
| `ButtonLOH` | 200 × 1 MB allocations on the Large Object Heap | < 1 s |
| `ButtonThrottled` | 1 MB streamed over 30 s (1 KB/s) | 30 s |

### Synthetic batches (emit 100 records each via `EmitSyntheticBatch`)

| Button | Records | Duration each |
|---|---|---|
| `Button4` (HTTP 500) | 100 × 500 | 120 ms |
| `Button400` / `Button401` / `Button403` / `Button404` | 100 × 4xx | 50 ms |
| `Button408` / `Button429` | 100 × 408/429 | 50 ms |
| `Button502` / `Button503` / `Button504` | 100 × 5xx | 80 ms |
| `ButtonSlow10x100` | 100 × 200 named `GET /Slow10.aspx` | 10 s |
| `ButtonSlow60x100` | 100 × 200 named `GET /Slow60.aspx` | 60 s |
| `ButtonMixedBurst` | 100 mixed (25 each of 500/502/503/504) | 80 ms |

> The batch buttons do **not** issue real HTTP. They write `RequestTelemetry`
> rows directly to AI. To filter them out: `where customDimensions.synthetic != "true"`.

### Worker-recycling (destructive — not wrapped)

These cannot be instrumented with `TrackInlineButton` because `Environment.FailFast`
and `StackOverflowException` skip `IDisposable.Dispose`:

- `Button1` — recursive call → `StackOverflowException`
- `Button2` — `while (true)` busy loop → 100% CPU
- `Button3` — unbounded `byte[]` allocation → `OutOfMemoryException`
- `ButtonFailFast` — `Environment.FailFast`
- `ButtonBgCrash` — unhandled exception on a background thread

### Customer-pattern scenarios (10 buttons — "Customer Scenarios" tab)

These reproduce real support patterns seen in production. Each handler is
wrapped in `TrackInlineButton` and emits **additional telemetry types**
(dependencies, customEvents, availabilityResults, exceptions) so a single
click produces a complete trace of the failure as a customer would see it.
All synthetic rows carry `customDimensions.synthetic = "true"` and a
`batchTag` ending in `-button` for filtering.

| Button | Status | Telemetry emitted |
|---|---|---|
| `ButtonDNSFail` | 500 | `dependencies` (HTTP, target=invalid host, resultCode=DnsFailure) + `exceptions` (`SocketException`) |
| `ButtonSqlConnectionPoolExhaust` | 500 | 30 × `dependencies` (SQL, target=`sqlserver.contoso-prod.database.windows.net \| OrdersDb`, 30 s, resultCode=-2) + 30 × `exceptions` (`InvalidOperationException` "Timeout expired … max pool size was reached") |
| `ButtonSqlSlowQuery` | 200 | 1 × `dependencies` (SQL, 35 s, success=true, missing-index `OPTION(MAXDOP 1)` query text) |
| `ButtonRedisTimeout` | 500 | 5 × `dependencies` (Redis, target=`perfscen-redis.redis.cache.windows.net:6380`, `GET user-session:{id}`, 5 s, resultCode=Timeout) + 5 × `exceptions` (`TimeoutException` with StackExchange.Redis-format message) |
| `ButtonInstanceIdentity` | 200 | 1 × `customEvents` (`AppServiceScenarios.InstanceIdentity`) with WEBSITE_INSTANCE_ID, REGION_NAME, SKU, machine, PID, processor count, CLR |
| `ButtonCustomEventBurst` | 200 | 1000 × `customEvents` (`AppServiceScenarios.UserAction`) with random userId/action/latency + 1 metric `AppServiceScenarios.BurstSize` |
| `ButtonAvailabilityTestEndpoint` | 200 | 10 × `availabilityResults` (`AppServiceScenarios-Availability`) across 5 regions, 8 success / 2 fail (timeout), spread over 5 min |
| `ButtonHealthCheckFailing` | 200 | 60 × `requests` (`GET /HealthCheck.aspx`, 503, 1 per second, batch=`HealthCheckFailing-button`) + 60 × `exceptions` |
| `ButtonTls10Only` | 500 | 1 × `dependencies` (HTTP, success=false) + `exceptions` (`AuthenticationException` "A call to SSPI failed … TLS 1.0 disabled by server policy") with `IOException` inner |
| `ButtonMissingAppSetting` | 500 | 1 × `exceptions` (`NullReferenceException`) with `customDimensions.missingKey = "AppServiceScenarios:RequiredEndpointUrl"` |

---

## App Service configuration

To make telemetry behave predictably, the deployment expects these app settings:

```bash
az webapp config appsettings set -g <rg> -n <app> --settings \
  APPLICATIONINSIGHTS_CONNECTION_STRING="<your-connstr>" \
  ApplicationInsightsAgent_EXTENSION_VERSION="~2" \
  XDT_MicrosoftApplicationInsights_Mode=default \
  MicrosoftAppInsights_AdaptiveSamplingEnabled=false \
  MicrosoftAppInsights_DependencyTrackingEnabled=true \
  MicrosoftAppInsights_RequestTrackingEnabled=true \
  APPINSIGHTS_PROFILERFEATURE_VERSION=disabled \
  APPINSIGHTS_SNAPSHOTFEATURE_VERSION=disabled
```

`MicrosoftAppInsights_AdaptiveSamplingEnabled=false` is the key one — without
it the codeless agent applies adaptive sampling and drops ~80 % of high-volume
batches.

---

## Build and deploy

```powershell
$msbuild = 'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe'
$pubDir  = (Resolve-Path .).Path + '\publish'

& $msbuild .\AppServiceScenarios.csproj `
  /t:Restore,Rebuild `
  /p:Configuration=Release `
  /p:DeployOnBuild=true `
  /p:WebPublishMethod=FileSystem `
  /p:DeployTarget=WebPublish `
  /p:publishUrl=$pubDir

Compress-Archive -Path .\publish\* -DestinationPath .\publish.zip -Force
az webapp deploy `
  --resource-group <rg> --name <app> `
  --src-path .\publish.zip --type zip --async false
```

---

## Verifying telemetry in App Insights

```kusto
// Single-page postbacks (last 15 min)
requests
| where timestamp > ago(15m) and name has "[Button"
| summarize cnt=count(),
            avg_s=round(avg(duration)/1000.0,1),
            max_s=round(max(duration)/1000.0,1)
       by name
| order by max_s desc
```

```kusto
// Synthetic batches grouped by tag
requests
| where timestamp > ago(15m)
| where tostring(customDimensions.synthetic) == "true"
| summarize cnt=count() by name, tostring(customDimensions.batch)
| order by cnt desc
```

```kusto
// Trace listener output
traces
| where timestamp > ago(15m) and message has "AppServiceScenarios"
| order by timestamp desc
```

```kusto
// Customer-scenario dependencies (SQL / Redis / HTTP)
dependencies
| where timestamp > ago(30m)
| where tostring(customDimensions.synthetic) == "true"
| summarize cnt=count(), avg_ms=round(avg(duration),0) by type, target, success
| order by cnt desc
```

```kusto
// Custom event burst + instance identity
customEvents
| where timestamp > ago(30m) and name startswith "AppServiceScenarios."
| summarize cnt=count() by name
```

```kusto
// Multi-region availability
availabilityResults
| where timestamp > ago(30m) and name == "AppServiceScenarios-Availability"
| summarize cnt=count(), succ=countif(success==true), fail=countif(success==false) by location
| order by location asc
```

```kusto
// Customer-scenario exceptions grouped by scenario
exceptions
| where timestamp > ago(30m)
| where tostring(customDimensions.batchTag) endswith "-button"
| summarize cnt=count() by type, scenario=tostring(customDimensions.scenario)
| order by cnt desc
```

---

## Driving traffic from PowerShell

`test-buttons.ps1` walks every button with VIEWSTATE-aware postbacks. For a
quick parallel slow-request burst:

```powershell
$base = 'https://<app>.azurewebsites.net/Default.aspx'
$page = Invoke-WebRequest -Uri $base -UseBasicParsing
$vs   = ([regex]'name="__VIEWSTATE" id="__VIEWSTATE" value="([^"]+)"').Match($page.Content).Groups[1].Value
$vsg  = ([regex]'name="__VIEWSTATEGENERATOR" id="__VIEWSTATEGENERATOR" value="([^"]+)"').Match($page.Content).Groups[1].Value
$ev   = ([regex]'name="__EVENTVALIDATION" id="__EVENTVALIDATION" value="([^"]+)"').Match($page.Content).Groups[1].Value
$form = @{'__VIEWSTATE'=$vs;'__VIEWSTATEGENERATOR'=$vsg;'__EVENTVALIDATION'=$ev;'ButtonSlow10'='ButtonSlow10'}

1..100 | ForEach-Object -Parallel {
    Invoke-WebRequest -Uri $using:base -Method Post -Body $using:form -UseBasicParsing -TimeoutSec 60 | Out-Null
} -ThrottleLimit 100
```

---

## Warning

Most of the buttons on this page are designed to break things:

- `Button1`, `Button2`, `Button3`, `ButtonFailFast`, `ButtonBgCrash` recycle the App Service worker.
- `ButtonMemoryLeak` permanently grows process memory until the worker is recycled.
- `ButtonSocketExhaust` consumes outbound SNAT ports for ~120 s.
- `ButtonThreadPoolStarve` saturates the threadpool for ~60 s.

Run only against a **disposable** App Service plan / app — never against
anything serving real traffic.

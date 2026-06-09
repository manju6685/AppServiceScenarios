using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace AppServiceScenarios
{
    public partial class Default : System.Web.UI.Page
    {
        // Static cache used by the gradual memory-leak scenario. Survives across requests
        // and grows by 100 MB each time the leak button is pressed.
        private static readonly List<byte[]> _leakedMemory = new List<byte[]>();
        private static readonly object _leakLock = new object();

        // ============================================================
        // Application Insights telemetry client.
        //
        // Why this exists: the codeless AI agent (~2) auto-captures inbound
        // HTTP requests, but it does NOT capture custom Trace.* output and it
        // does NOT capture work that happens on background threads (Task.Run).
        // The "100 HTTP 500 / 100 slow / 100 4xx" buttons all run on background
        // threads, so without this client their work is invisible in AI.
        //
        // We use TelemetryClient to emit RequestTelemetry directly. Each batch
        // button records 100 synthetic request rows tagged synthetic=true so
        // they show up in the Failures / Performance blades exactly like real
        // traffic would.
        // ============================================================
        private static readonly TelemetryClient _ai = CreateAiClient();

        private static TelemetryClient CreateAiClient()
        {
            var cfg = TelemetryConfiguration.CreateDefault();
            var conn = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
            if (!string.IsNullOrEmpty(conn))
            {
                cfg.ConnectionString = conn;
            }
            return new TelemetryClient(cfg);
        }

        // Singleton HttpClient used by the load-test button to fire many concurrent
        // loopback requests without exhausting sockets. ServicePointManager.DefaultConnectionLimit
        // defaults to 2 on .NET Framework, which would serialize the burst — bump it.
        private static readonly HttpClient _loadTestHttp = CreateLoadTestHttpClient();

        private static HttpClient CreateLoadTestHttpClient()
        {
            ServicePointManager.DefaultConnectionLimit = 1000;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("AppServiceScenarios-LoadTest/1.0");
            return c;
        }

        /// <summary>
        /// Emit one synthetic RequestTelemetry record (and an ExceptionTelemetry
        /// for non-2xx/3xx codes) so the work shows up in App Insights even
        /// though no real inbound HTTP request was made.
        /// </summary>
        private static void EmitSyntheticRequest(
            string baseUrl, string pageName, int statusCode, int durationMs,
            int index, string batchTag)
        {
            bool success = statusCode >= 200 && statusCode < 400;
            var rt = new RequestTelemetry
            {
                Name = $"GET /{pageName}",
                Url = new Uri($"{baseUrl}/{pageName}"),
                Timestamp = DateTimeOffset.UtcNow,
                ResponseCode = statusCode.ToString(),
                Success = success,
                Duration = TimeSpan.FromMilliseconds(durationMs)
            };
            rt.Properties["synthetic"] = "true";
            rt.Properties["batch"] = batchTag;
            rt.Properties["index"] = (index + 1).ToString();
            _ai.TrackRequest(rt);

            if (!success)
            {
                var et = new ExceptionTelemetry(new InvalidOperationException(
                    $"Synthetic HTTP {statusCode} from {pageName} #{index + 1}"))
                {
                    Timestamp = rt.Timestamp
                };
                et.Properties["synthetic"] = "true";
                et.Properties["batch"] = batchTag;
                et.Properties["resultCode"] = statusCode.ToString();
                _ai.TrackException(et);
            }
        }

        /// <summary>Emit a batch of synthetic request rows on a background task.</summary>
        private void EmitSyntheticBatch(string pageName, int statusCode, int count, int durationMs, string batchTag)
        {
            var baseUrl = Request.Url.GetLeftPart(UriPartial.Authority);
            Task.Run(() =>
            {
                System.Diagnostics.Trace.TraceInformation(
                    $"[AppServiceScenarios] {batchTag}: emitting {count} x HTTP {statusCode} ({pageName}) to AI");
                for (int i = 0; i < count; i++)
                {
                    EmitSyntheticRequest(baseUrl, pageName, statusCode, durationMs, i, batchTag);
                }
                _ai.Flush();
                System.Diagnostics.Trace.TraceInformation(
                    $"[AppServiceScenarios] {batchTag}: flushed {count} records to AI.");
            });
        }

        /// <summary>
        /// Wrap the body of a single-page button handler so that AI gets a
        /// RequestTelemetry record tagged with the button name. The codeless
        /// agent on .NET Framework doesn't reliably auto-capture POST /Default.aspx
        /// postbacks from the worker — we emit them ourselves.
        /// </summary>
        private IDisposable TrackInlineButton(string buttonName, int statusCode = 200)
        {
            return new InlineRequestEmitter(_ai, buttonName, Request.Url, statusCode);
        }

        private sealed class InlineRequestEmitter : IDisposable
        {
            private readonly TelemetryClient _client;
            private readonly string _buttonName;
            private readonly Uri _url;
            private readonly int _statusCode;
            private readonly DateTimeOffset _start;
            private readonly System.Diagnostics.Stopwatch _sw;

            public InlineRequestEmitter(TelemetryClient client, string buttonName, Uri url, int statusCode)
            {
                _client = client;
                _buttonName = buttonName;
                _url = url;
                _statusCode = statusCode;
                _start = DateTimeOffset.UtcNow;
                _sw = System.Diagnostics.Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _sw.Stop();
                bool success = _statusCode >= 200 && _statusCode < 400;
                var rt = new RequestTelemetry
                {
                    Name = "POST /Default.aspx [" + _buttonName + "]",
                    Url = _url,
                    Timestamp = _start,
                    ResponseCode = _statusCode.ToString(),
                    Success = success,
                    Duration = _sw.Elapsed
                };
                rt.Properties["button"] = _buttonName;
                rt.Properties["synthetic"] = "true";
                rt.Properties["source"] = "InlineButton";
                _client.TrackRequest(rt);
                _client.Flush();
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
        }

        protected void Button1_Click(object sender, EventArgs e)
        {
            // This will cause a StackOverflowException
            CauseStackOverflow();
        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            // This will cause high CPU usage
            CauseHighCPU();
        }

        protected void Button3_Click(object sender, EventArgs e)
        {
            // This will cause high memory usage
            CauseHighMemory();
        }
       
        private void CauseStackOverflow()
        {
            CauseStackOverflow(); // Recursive call without a termination condition
        }

        private void CauseHighCPU()
        {
            while (true)
            {
                // Busy-wait loop to cause high CPU usage
            }
        }

        private void CauseHighMemory()
        {
            List<byte[]> memoryHog = new List<byte[]>();
            while (true)
            {
                // Allocate large chunks of memory in a loop
                memoryHog.Add(new byte[1024 * 1024]); // Allocate 1 MB
            }
        }

        protected void Button4_Click(object sender, EventArgs e)
        {
            // Generate 100 HTTP 500 entries in App Insights. We emit RequestTelemetry
            // directly via TelemetryClient instead of doing 100 outbound HTTPS
            // self-callbacks (which were silently failing due to TLS/SNI from the
            // worker back to its own public hostname).
            EmitSyntheticBatch("Http500.aspx", statusCode: 500, count: 100, durationMs: 120, batchTag: "Http500-batch");

            StatusPanel.Visible = true;
            StatusTitle.Text = "HTTP 500 Generation Started";
            StatusMessage.Text = "100 HTTP 500 server errors are being recorded in Application Insights. Refresh the AI Failures blade in ~30s.";
        }

        protected void BackButton_Click(object sender, EventArgs e)
        {
            StatusPanel.Visible = false;
        }

        protected void ButtonSlow10_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonSlow10"))
            {
                Thread.Sleep(10000); // Sleep for 10 seconds

                // Show success message
                StatusPanel.Visible = true;
                StatusTitle.Text = "10 Second Delay Test Completed";
                StatusMessage.Text = "Successfully simulated a 10-second response delay.";
            }
        }

        protected void ButtonSlow60_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonSlow60"))
            {
                Thread.Sleep(60000); // Sleep for 60 seconds

                // Show success message
                StatusPanel.Visible = true;
                StatusTitle.Text = "60 Second Delay Test Completed";
                StatusMessage.Text = "Successfully simulated a 60-second response delay.";
            }
        }

        protected void Button400_Click(object sender, EventArgs e)
        {
            EmitSyntheticBatch("BadRequest.aspx", 400, 100, 50, "Http400-batch");
            StatusPanel.Visible = true;
            StatusTitle.Text = "Successfully Generated HTTP 400 Errors";
            StatusMessage.Text = "100 HTTP 400 Bad Request entries are being recorded in Application Insights.";
        }

        protected void Button401_Click(object sender, EventArgs e)
        {
            EmitSyntheticBatch("Unauthorized.aspx", 401, 100, 50, "Http401-batch");
            StatusPanel.Visible = true;
            StatusTitle.Text = "Successfully Generated HTTP 401 Errors";
            StatusMessage.Text = "100 HTTP 401 Unauthorized entries are being recorded in Application Insights.";
        }

        protected void Button403_Click(object sender, EventArgs e)
        {
            EmitSyntheticBatch("Forbidden.aspx", 403, 100, 50, "Http403-batch");
            StatusPanel.Visible = true;
            StatusTitle.Text = "Successfully Generated HTTP 403 Errors";
            StatusMessage.Text = "100 HTTP 403 Forbidden entries are being recorded in Application Insights.";
        }

        protected void Button404_Click(object sender, EventArgs e)
        {
            EmitSyntheticBatch("NotFound.aspx", 404, 100, 50, "Http404-batch");
            StatusPanel.Visible = true;
            StatusTitle.Text = "Successfully Generated HTTP 404 Errors";
            StatusMessage.Text = "100 HTTP 404 Not Found entries are being recorded in Application Insights.";
        }

        protected void ButtonSlow10x100_Click(object sender, EventArgs e)
        {
            // 100 synthetic 'slow' requests: 10s duration each, status 200.
            EmitSyntheticBatch("Slow10.aspx", statusCode: 200, count: 100, durationMs: 10000, batchTag: "Slow10-batch");

            StatusPanel.Visible = true;
            StatusTitle.Text = "Slow Request Test Started";
            StatusMessage.Text = "100 slow requests (10 seconds each) are being recorded in Application Insights. Check the Performance blade in ~30s.";
        }

        protected void ButtonSlow60x100_Click(object sender, EventArgs e)
        {
            // 100 synthetic 'slow' requests: 60s duration each, status 200.
            EmitSyntheticBatch("Slow60.aspx", statusCode: 200, count: 100, durationMs: 60000, batchTag: "Slow60-batch");

            StatusPanel.Visible = true;
            StatusTitle.Text = "Slow Request Test Started";
            StatusMessage.Text = "100 slow requests (60 seconds each) are being recorded in Application Insights. Check the Performance blade in ~30s.";
        }

        /// <summary>
        /// Fires N REAL parallel HTTP GETs against /Slow10.aspx using HttpClient.
        /// Unlike EmitSyntheticBatch (which only writes to App Insights), this generates
        /// real inbound traffic through IIS — so the App Service auto-heal rule
        /// (10 requests &gt; 7s in 60s) actually counts them and triggers the
        /// configured action (DaaS CLR Profiler).
        /// Fires fire-and-forget on a background Task so the button returns immediately.
        /// </summary>
        protected void ButtonSlow10ParallelReal_Click(object sender, EventArgs e)
        {
            FireParallelRealRequests("Slow10.aspx", "Slow10-parallel-real");
            StatusPanel.Visible = true;
            StatusTitle.Text = "100 Parallel Real Slow Requests Started";
            StatusMessage.Text = "Firing 100 real parallel HTTP GETs against /Slow10.aspx (~10s each). " +
                                 "Auto-heal slow-request rule should trigger within ~15s. " +
                                 "Check Kudu: /api/vfs/data/DaaS/Sessions/ for a new session, or the Auto-Heal History tab in the portal.";
        }

        /// <summary>
        /// Fires N REAL parallel HTTP GETs against /Http500.aspx using HttpClient.
        /// Triggers the auto-heal status-code rule (10 reqs HTTP 500 in 60s).
        /// </summary>
        protected void ButtonHttp500ParallelReal_Click(object sender, EventArgs e)
        {
            FireParallelRealRequests("Http500.aspx", "Http500-parallel-real");
            StatusPanel.Visible = true;
            StatusTitle.Text = "100 Parallel Real HTTP 500 Requests Started";
            StatusMessage.Text = "Firing 100 real parallel HTTP GETs against /Http500.aspx. " +
                                 "Auto-heal status-code rule should trigger within ~5s. " +
                                 "Check Kudu: /api/vfs/data/DaaS/Sessions/ for a new session, or the Auto-Heal History tab in the portal.";
        }

        /// <summary>
        /// Shared helper: fires 100 parallel real HTTP GETs against the given page
        /// on this same app. Runs fire-and-forget on a background Task so the
        /// button postback returns immediately.
        /// </summary>
        private void FireParallelRealRequests(string pageName, string batchTag, int parallelCount = 100)
        {
            var baseUrl = Request.Url.GetLeftPart(UriPartial.Authority);
            var target = $"{baseUrl}/{pageName}";

            Task.Run(async () =>
            {
                System.Diagnostics.Trace.TraceInformation(
                    $"[AppServiceScenarios] {batchTag}: firing {parallelCount} real parallel GETs against {target}");

                var tasks = new Task[parallelCount];
                for (int i = 0; i < parallelCount; i++)
                {
                    int idx = i;
                    tasks[i] = Task.Run(async () =>
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        try
                        {
                            using (var resp = await _loadTestHttp.GetAsync(target).ConfigureAwait(false))
                            {
                                sw.Stop();
                                System.Diagnostics.Trace.TraceInformation(
                                    $"[AppServiceScenarios] {batchTag} #{idx + 1}: HTTP {(int)resp.StatusCode} in {sw.ElapsedMilliseconds}ms");
                            }
                        }
                        catch (Exception ex)
                        {
                            sw.Stop();
                            System.Diagnostics.Trace.TraceError(
                                $"[AppServiceScenarios] {batchTag} #{idx + 1}: failed after {sw.ElapsedMilliseconds}ms — {ex.Message}");
                        }
                    });
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
                System.Diagnostics.Trace.TraceInformation(
                    $"[AppServiceScenarios] {batchTag}: all {parallelCount} requests completed.");
            });
        }

        // ============================================================
        // HTTP 4xx / 5xx additional generators
        // ============================================================

        protected void Button429_Click(object sender, EventArgs e)
        {
            EmitSyntheticBatch("TooManyRequests.aspx", 429, 100, 50, "Http429-batch");
            StatusPanel.Visible = true;
            StatusTitle.Text = "Successfully Generated HTTP 429 Errors";
            StatusMessage.Text = "100 HTTP 429 throttling responses are being recorded in Application Insights.";
        }

        protected void Button502_Click(object sender, EventArgs e)
        {
            EmitSyntheticBatch("BadGateway.aspx", 502, 100, 80, "Http502-batch");
            StatusPanel.Visible = true;
            StatusTitle.Text = "Successfully Generated HTTP 502 Errors";
            StatusMessage.Text = "100 HTTP 502 Bad Gateway responses are being recorded in Application Insights.";
        }

        protected void Button503_Click(object sender, EventArgs e)
        {
            EmitSyntheticBatch("ServiceUnavailable.aspx", 503, 100, 80, "Http503-batch");
            StatusPanel.Visible = true;
            StatusTitle.Text = "Successfully Generated HTTP 503 Errors";
            StatusMessage.Text = "100 HTTP 503 Service Unavailable responses are being recorded in Application Insights.";
        }

        protected void Button504_Click(object sender, EventArgs e)
        {
            EmitSyntheticBatch("GatewayTimeout.aspx", 504, 100, 80, "Http504-batch");
            StatusPanel.Visible = true;
            StatusTitle.Text = "Successfully Generated HTTP 504 Errors";
            StatusMessage.Text = "100 HTTP 504 Gateway Timeout responses are being recorded in Application Insights.";
        }

        // ============================================================
        // Advanced scenarios
        // ============================================================

        protected void ButtonThreadPoolStarve_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonThreadPoolStarve"))
            {
                // Queue 200 long blocking sleeps to the threadpool. On a default App Service
                // worker this saturates available worker threads and causes incoming requests
                // to queue, surfacing as latency / 503s upstream.
                for (int i = 0; i < 200; i++)
                {
                    ThreadPool.QueueUserWorkItem(_ => Thread.Sleep(60000));
                }

                StatusPanel.Visible = true;
                StatusTitle.Text = "ThreadPool Starvation Triggered";
                StatusMessage.Text = "Queued 200 blocking work items (60s each). Worker threadpool will be saturated for ~1 minute. Watch for queued requests / 503s.";
            }
        }

        protected void ButtonDeadlock_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonDeadlock"))
            {
                // Classic sync-over-async deadlock pattern. The async continuation tries to
                // resume on the captured ASP.NET request context but the request thread is
                // blocked on .Result, so neither side ever completes.
                // We start it on a background task (with a watchdog) so the *current* request
                // doesn't hang forever — but the deadlocked task will hold a thread.
                Task.Run(() =>
                {
                    try
                    {
                        var result = DeadlockingMethodAsync().Result;
                        System.Diagnostics.Trace.TraceInformation($"Deadlock task unexpectedly returned: {result}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError($"Deadlock task observed: {ex.Message}");
                    }
                });

                StatusPanel.Visible = true;
                StatusTitle.Text = "Deadlock Triggered";
                StatusMessage.Text = "A sync-over-async deadlock has been triggered on a background thread. That thread will be stuck indefinitely until the worker is recycled.";
            }
        }

        private static async Task<int> DeadlockingMethodAsync()
        {
            await Task.Delay(50).ConfigureAwait(true); // require the captured context
            return 42;
        }

        protected void ButtonMemoryLeak_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonMemoryLeak"))
            {
                // Add 100 MB to a process-wide static cache. Repeated clicks accumulate.
                const int chunkSizeBytes = 1024 * 1024; // 1 MB
                const int chunks = 100;                  // 100 MB per click

                lock (_leakLock)
                {
                    for (int i = 0; i < chunks; i++)
                    {
                        var buffer = new byte[chunkSizeBytes];
                        // Touch every page so the OS actually backs it with physical memory.
                        for (int j = 0; j < buffer.Length; j += 4096)
                        {
                            buffer[j] = 1;
                        }
                        _leakedMemory.Add(buffer);
                    }
                }

                long totalMb;
                lock (_leakLock)
                {
                    totalMb = (long)_leakedMemory.Count * chunkSizeBytes / (1024 * 1024);
                }

                StatusPanel.Visible = true;
                StatusTitle.Text = "Memory Leak Increment Added";
                StatusMessage.Text = $"Added 100 MB to the static cache. Total leaked memory in this process: {totalMb} MB. Click again to grow further.";
            }
        }

        protected void ButtonLargePayload_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonLargePayload"))
            {
                // Stream ~10 MB of text back to the caller without buffering, to test
                // bandwidth handling, response timeouts, and front-end buffering.
                const int totalBytes = 10 * 1024 * 1024;
                const int chunkSize = 64 * 1024;
                var chunk = new string('A', chunkSize);

                Response.Clear();
                Response.ContentType = "text/plain";
                Response.BufferOutput = false;

                int written = 0;
                while (written < totalBytes)
                {
                    Response.Write(chunk);
                    Response.Flush();
                    written += chunkSize;
                }

                Response.End();
            }
        }

        protected void ButtonOutboundHang_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonOutboundHang"))
            {
                // Call an unroutable host to simulate a hung downstream dependency.
                // 192.0.2.0/24 is reserved by RFC5737 for documentation/testing and is
                // guaranteed not to respond.
                const string unreachable = "http://192.0.2.1/";
                var sw = System.Diagnostics.Stopwatch.StartNew();
                string outcome;
                try
                {
                    var webRequest = (HttpWebRequest)WebRequest.Create(unreachable);
                    webRequest.Method = "GET";
                    webRequest.Timeout = 30000; // 30s

                    using (var response = webRequest.GetResponse())
                    {
                        outcome = $"Unexpected success: {((HttpWebResponse)response).StatusCode}";
                    }
                }
                catch (WebException webEx)
                {
                    outcome = $"WebException after {sw.ElapsedMilliseconds} ms: {webEx.Status} - {webEx.Message}";
                    System.Diagnostics.Trace.TraceError(outcome);
                }
                catch (Exception ex)
                {
                    outcome = $"Exception after {sw.ElapsedMilliseconds} ms: {ex.Message}";
                    System.Diagnostics.Trace.TraceError(outcome);
                }

                StatusPanel.Visible = true;
                StatusTitle.Text = "Outbound Dependency Hang Completed";
                StatusMessage.Text = outcome;
            }
        }

        protected void ButtonDiskIO_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonDiskIO"))
            {
                // Write 10 x 50 MB files to %TEMP%, then delete them. Stresses local
                // disk on the App Service worker (D:\local\Temp).
                const int fileCount = 10;
                const int fileSizeBytes = 50 * 1024 * 1024;
                const int bufferSize = 1024 * 1024;
                var buffer = new byte[bufferSize];
                var random = new Random();
                random.NextBytes(buffer);

                var tempDir = Path.Combine(Path.GetTempPath(), "AppServiceScenariosDiskIO");
                Directory.CreateDirectory(tempDir);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var written = new List<string>(fileCount);
                try
                {
                    for (int i = 0; i < fileCount; i++)
                    {
                        var path = Path.Combine(tempDir, $"perf_{Guid.NewGuid():N}.bin");
                        using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize, FileOptions.WriteThrough))
                        {
                            int writtenBytes = 0;
                            while (writtenBytes < fileSizeBytes)
                            {
                                fs.Write(buffer, 0, bufferSize);
                                writtenBytes += bufferSize;
                            }
                            fs.Flush(true);
                        }
                        written.Add(path);
                    }
                }
                finally
                {
                    foreach (var p in written)
                    {
                        try { File.Delete(p); } catch { /* best effort */ }
                    }
                }

                StatusPanel.Visible = true;
                StatusTitle.Text = "Disk I/O Test Completed";
                StatusMessage.Text = $"Wrote and deleted {fileCount} x 50 MB files (~500 MB total) in {sw.ElapsedMilliseconds} ms.";
            }
        }

        protected void ButtonFailFast_Click(object sender, EventArgs e)
        {
            // Hard-terminate the worker process. App Service will restart the worker.
            // This is distinct from the StackOverflow scenario - it's an immediate,
            // clean process kill with a recorded reason.
            System.Diagnostics.Trace.TraceError("Triggering Environment.FailFast from AppServiceScenarios");
            Environment.FailFast("AppServiceScenarios: ButtonFailFast_Click triggered hard process crash");
        }

        // ============================================================
        // App Service-specific scenarios
        // ============================================================

        protected void Button408_Click(object sender, EventArgs e)
        {
            EmitSyntheticBatch("RequestTimeout.aspx", 408, 100, 50, "Http408-batch");
            StatusPanel.Visible = true;
            StatusTitle.Text = "Successfully Generated HTTP 408 Errors";
            StatusMessage.Text = "100 HTTP 408 Request Timeout responses are being recorded in Application Insights.";
        }

        protected void ButtonBgCrash_Click(object sender, EventArgs e)
        {
            // Throw an unhandled exception on a background thread. On classic .NET
            // Framework hosting (App Service Windows), this will recycle w3wp.exe
            // because legacyUnhandledExceptionPolicy is "false" by default for ASP.NET.
            // We delay slightly so the response can return before the worker dies.
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(500);
                System.Diagnostics.Trace.TraceError("AppServiceScenarios: throwing unhandled exception on background thread");
                throw new InvalidOperationException(
                    "AppServiceScenarios: simulated unhandled background-thread exception. " +
                    "This will recycle w3wp.exe on classic ASP.NET hosting.");
            });

            StatusPanel.Visible = true;
            StatusTitle.Text = "Background Thread Crash Scheduled";
            StatusMessage.Text = "An unhandled exception will be thrown on a background thread in ~500ms. The App Service worker will be recycled.";
        }

        protected void ButtonSocketExhaust_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonSocketExhaust"))
            {
                // Anti-pattern: create many short-lived HttpClients. Each disposed client
                // leaves its underlying TCP socket in TIME_WAIT for ~120s, exhausting
                // ephemeral ports under load. This reproduces the classic SNAT port
                // exhaustion symptoms seen on Azure App Service.
                var baseUrl = Request.Url.GetLeftPart(UriPartial.Authority);
                const int iterations = 200;

                Task.Run(() =>
                {
                    int success = 0, failed = 0;
                    for (int i = 0; i < iterations; i++)
                    {
                        try
                        {
                            // INTENTIONALLY using a new HttpClient per request - the wrong way.
                            using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                            {
                                var resp = client.GetAsync($"{baseUrl}/FastResponse.aspx").Result;
                                if (resp.IsSuccessStatusCode) success++; else failed++;
                            }
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            System.Diagnostics.Trace.TraceError($"Socket-exhaust iter {i}: {ex.Message}");
                        }
                    }
                    System.Diagnostics.Trace.TraceInformation(
                        $"Socket-exhaust pass complete. success={success} failed={failed}");
                });

                StatusPanel.Visible = true;
                StatusTitle.Text = "HttpClient Socket Exhaustion Started";
                StatusMessage.Text = $"Spawning {iterations} short-lived HttpClient instances in the background. Each leaves a TIME_WAIT socket. Watch netstat / SNAT port metrics on App Service.";
            }
        }

        protected void ButtonLOH_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonLOH"))
            {
                // Allocate 200 x 1 MB byte arrays. Anything > 85,000 bytes goes on the
                // Large Object Heap and is collected only by Gen2. This produces visible
                // GC pauses that manifest as latency spikes on App Service.
                var sw = System.Diagnostics.Stopwatch.StartNew();
                const int chunks = 200;
                const int sizeBytes = 1024 * 1024;
                long beforeGen2 = GC.CollectionCount(2);

                // Hold them just long enough to force fragmentation; let GC clean up after.
                var holder = new List<byte[]>(chunks);
                for (int i = 0; i < chunks; i++)
                {
                    holder.Add(new byte[sizeBytes]);
                }
                holder = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                long afterGen2 = GC.CollectionCount(2);

                StatusPanel.Visible = true;
                StatusTitle.Text = "LOH Pressure Test Completed";
                StatusMessage.Text = $"Allocated {chunks} × 1 MB on the Large Object Heap. Gen2 collections delta: {afterGen2 - beforeGen2}. Total elapsed: {sw.ElapsedMilliseconds} ms.";
            }
        }

        protected void ButtonThrottled_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonThrottled"))
            {
                // Stream 1 MB over 30s — 30 chunks of ~33 KB with 1s pause.
                // Tests Always On / front-end / TCP idle timeouts.
                const int totalChunks = 30;
                const int chunkSize = 33 * 1024;
                var chunk = new string('B', chunkSize);

                Response.Clear();
                Response.ContentType = "text/plain";
                Response.BufferOutput = false;

                for (int i = 0; i < totalChunks; i++)
                {
                    if (!Response.IsClientConnected) break;
                    Response.Write($"chunk {i + 1}/{totalChunks}: ");
                    Response.Write(chunk);
                    Response.Write("\r\n");
                    Response.Flush();
                    Thread.Sleep(1000);
                }

                Response.End();
            }
        }

        protected void ButtonMixedBurst_Click(object sender, EventArgs e)
        {
            // 100 mixed 5xx records (25 each of 500, 502, 503, 504) emitted directly
            // to App Insights via TelemetryClient. No outbound HTTP — it was unreliable.
            var baseUrl = Request.Url.GetLeftPart(UriPartial.Authority);
            int[] statuses = { 500, 502, 503, 504 };

            Task.Run(() =>
            {
                System.Diagnostics.Trace.TraceInformation(
                    "[AppServiceScenarios] MixedBurst: emitting 100 mixed 5xx records to AI");
                for (int i = 0; i < 100; i++)
                {
                    int code = statuses[i % statuses.Length];
                    EmitSyntheticRequest(baseUrl, "HealthCheck.aspx", code, 80, i, "MixedBurst-batch");
                }
                _ai.Flush();
                System.Diagnostics.Trace.TraceInformation(
                    "[AppServiceScenarios] MixedBurst: flushed 100 records to AI.");
            });

            StatusPanel.Visible = true;
            StatusTitle.Text = "Mixed Error Burst Started";
            StatusMessage.Text = "Recording 100 mixed 5xx responses (25 each of 500/502/503/504) in Application Insights.";
        }

        // ----------------------------------------------------------------------
        // Customer-scenario buttons (DNS, SQL, Redis, TLS, identity, etc.)
        // ----------------------------------------------------------------------

        private void EmitSyntheticDependency(string type, string target, string name, string data,
            int durationMs, bool success, string resultCode, string batchTag)
        {
            var dt = new DependencyTelemetry
            {
                Type = type,
                Target = target,
                Name = name,
                Data = data,
                Timestamp = DateTimeOffset.UtcNow.AddMilliseconds(-durationMs),
                Duration = TimeSpan.FromMilliseconds(durationMs),
                Success = success,
                ResultCode = resultCode
            };
            dt.Properties["batchTag"] = batchTag;
            dt.Properties["synthetic"] = "true";
            _ai.TrackDependency(dt);
        }

        protected void ButtonDNSFail_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonDNSFail", 500))
            {
                const string badHost = "does-not-exist-AppServiceScenarios-12345.invalid";
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Exception caught = null;
                try
                {
                    System.Net.Dns.GetHostEntry(badHost);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                sw.Stop();

                EmitSyntheticDependency(
                    type: "Http",
                    target: badHost,
                    name: "DNS resolve",
                    data: "Dns.GetHostEntry(\"" + badHost + "\")",
                    durationMs: (int)sw.ElapsedMilliseconds,
                    success: false,
                    resultCode: caught is SocketException se ? ((int)se.SocketErrorCode).ToString() : "DnsFailure",
                    batchTag: "DNSFail-button");

                if (caught != null)
                {
                    var et = new ExceptionTelemetry(caught);
                    et.Properties["batchTag"] = "DNSFail-button";
                    et.Properties["scenario"] = "DNS resolution failure";
                    _ai.TrackException(et);
                }

                StatusPanel.Visible = true;
                StatusTitle.Text = "DNS Resolution Failure";
                StatusMessage.Text = "Tried to resolve '" + badHost + "' — got: " +
                    (caught == null ? "(unexpectedly succeeded)" : caught.GetType().Name + ": " + caught.Message);
            }
        }

        protected void ButtonSqlConnectionPoolExhaust_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonSqlConnectionPoolExhaust", 500))
            {
                const string server = "sqlserver.contoso-prod.database.windows.net";
                const string db = "OrdersDb";
                const string target = server + " | " + db;
                Task.Run(() =>
                {
                    System.Diagnostics.Trace.TraceWarning(
                        "[AppServiceScenarios] SqlConnectionPoolExhaust: emitting 30 pool-exhaustion dependencies");
                    for (int i = 0; i < 30; i++)
                    {
                        EmitSyntheticDependency(
                            type: "SQL",
                            target: target,
                            name: "OrdersDb.dbo.GetOrdersByCustomer",
                            data: "EXEC dbo.GetOrdersByCustomer @customerId = " + (1000 + i),
                            durationMs: 30000,
                            success: false,
                            resultCode: "-2",
                            batchTag: "SqlPoolExhaust-button");

                        var ex = new InvalidOperationException(
                            "Timeout expired. The timeout period elapsed prior to obtaining a connection " +
                            "from the pool. This may have occurred because all pooled connections were in " +
                            "use and max pool size was reached.");
                        var et = new ExceptionTelemetry(ex);
                        et.Properties["batchTag"] = "SqlPoolExhaust-button";
                        et.Properties["scenario"] = "SQL connection pool exhaustion";
                        et.Properties["target"] = target;
                        _ai.TrackException(et);
                    }
                    _ai.Flush();
                });

                StatusPanel.Visible = true;
                StatusTitle.Text = "SQL Connection Pool Exhaustion";
                StatusMessage.Text = "Recording 30 SQL pool-exhaustion dependency failures + matching exceptions to '" + target + "'.";
            }
        }

        protected void ButtonSqlSlowQuery_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonSqlSlowQuery"))
            {
                const string server = "sqlserver.contoso-prod.database.windows.net";
                const string db = "OrdersDb";
                const string target = server + " | " + db;

                EmitSyntheticDependency(
                    type: "SQL",
                    target: target,
                    name: "OrdersDb.dbo.ReportFullScan",
                    data: "SELECT COUNT_BIG(*) FROM dbo.Orders o " +
                          "JOIN dbo.OrderLines l ON l.OrderId = o.Id " +
                          "WHERE o.CreatedUtc > DATEADD(year,-5,GETUTCDATE()) " +
                          "OPTION (MAXDOP 1) /* missing index on CreatedUtc */",
                    durationMs: 35000,
                    success: true,
                    resultCode: "0",
                    batchTag: "SqlSlowQuery-button");

                StatusPanel.Visible = true;
                StatusTitle.Text = "SQL Slow Query";
                StatusMessage.Text = "Recorded a 35-second slow SQL dependency to '" + target + "' (success=true, missing index pattern).";
            }
        }

        protected void ButtonRedisTimeout_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonRedisTimeout", 500))
            {
                const string target = "perfscen-redis.redis.cache.windows.net:6380";
                var rnd = new Random();
                for (int i = 0; i < 5; i++)
                {
                    int sessionId = rnd.Next(1000, 9999);
                    EmitSyntheticDependency(
                        type: "Redis",
                        target: target,
                        name: "GET",
                        data: "GET user-session:" + sessionId,
                        durationMs: 5000,
                        success: false,
                        resultCode: "Timeout",
                        batchTag: "RedisTimeout-button");

                    var ex = new TimeoutException(
                        "Timeout performing GET (5000ms), inst: 1, qu: 0, qs: 0, aw: True, " +
                        "rs: ReadAsync, ws: Idle, in: 0, serverEndpoint: " + target +
                        ", mc: 1/1/0, mgr: 10 of 10 available, clientName: AppServiceScenarios");
                    var et = new ExceptionTelemetry(ex);
                    et.Properties["batchTag"] = "RedisTimeout-button";
                    et.Properties["scenario"] = "Redis StackExchange.Redis timeout";
                    et.Properties["target"] = target;
                    _ai.TrackException(et);
                }

                StatusPanel.Visible = true;
                StatusTitle.Text = "Redis Timeouts";
                StatusMessage.Text = "Recorded 5 Redis GET timeouts (5s each) against '" + target + "' with matching TimeoutException records.";
            }
        }

        protected void ButtonInstanceIdentity_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonInstanceIdentity"))
            {
                string instanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? "(not set)";
                string siteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "(not set)";
                string region = Environment.GetEnvironmentVariable("REGION_NAME") ?? "(not set)";
                string sku = Environment.GetEnvironmentVariable("WEBSITE_SKU") ?? "(not set)";
                string machine = Environment.MachineName;
                int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                int procCount = Environment.ProcessorCount;
                string clrVersion = Environment.Version.ToString();

                var et = new EventTelemetry("AppServiceScenarios.InstanceIdentity");
                et.Properties["WEBSITE_INSTANCE_ID"] = instanceId;
                et.Properties["WEBSITE_SITE_NAME"] = siteName;
                et.Properties["REGION_NAME"] = region;
                et.Properties["WEBSITE_SKU"] = sku;
                et.Properties["MachineName"] = machine;
                et.Properties["ProcessId"] = pid.ToString();
                et.Properties["ProcessorCount"] = procCount.ToString();
                et.Properties["CLRVersion"] = clrVersion;
                _ai.TrackEvent(et);

                StatusPanel.Visible = true;
                StatusTitle.Text = "Instance Identity";
                StatusMessage.Text = string.Format(
                    "Site: {0} | Instance: {1} | Region: {2} | SKU: {3} | Machine: {4} | PID: {5} | Cores: {6} | CLR: {7}",
                    siteName, instanceId, region, sku, machine, pid, procCount, clrVersion);
            }
        }

        protected void ButtonCustomEventBurst_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonCustomEventBurst"))
            {
                Task.Run(() =>
                {
                    var rnd = new Random();
                    string[] actions = { "AddToCart", "Checkout", "Login", "Logout", "Search", "ViewProduct" };
                    System.Diagnostics.Trace.TraceInformation(
                        "[AppServiceScenarios] CustomEventBurst: emitting 1000 custom events");
                    for (int i = 0; i < 1000; i++)
                    {
                        var et = new EventTelemetry("AppServiceScenarios.UserAction");
                        et.Properties["userId"] = "user-" + rnd.Next(1, 5000);
                        et.Properties["action"] = actions[rnd.Next(actions.Length)];
                        et.Properties["batchTag"] = "CustomEventBurst-button";
                        et.Metrics["latencyMs"] = rnd.Next(20, 500);
                        _ai.TrackEvent(et);
                    }
                    _ai.GetMetric("AppServiceScenarios.BurstSize").TrackValue(1000);
                    _ai.Flush();
                });

                StatusPanel.Visible = true;
                StatusTitle.Text = "Custom Event Burst";
                StatusMessage.Text = "Emitting 1000 'AppServiceScenarios.UserAction' custom events with random userId/action and a metric record.";
            }
        }

        protected void ButtonAvailabilityTestEndpoint_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonAvailabilityTestEndpoint"))
            {
                Task.Run(() =>
                {
                    string runId = Guid.NewGuid().ToString("N").Substring(0, 8);
                    var rnd = new Random();
                    System.Diagnostics.Trace.TraceInformation(
                        "[AppServiceScenarios] AvailabilityTestEndpoint: emitting 10 availability rows");
                    string[] locations = { "East US", "West US 2", "West Europe", "Southeast Asia", "Australia East" };

                    for (int i = 0; i < 10; i++)
                    {
                        bool ok = (i != 3 && i != 7); // 8 success, 2 failure
                        int durationMs = ok ? rnd.Next(150, 600) : 30000;
                        var at = new AvailabilityTelemetry
                        {
                            Name = "AppServiceScenarios-Availability",
                            RunLocation = locations[i % locations.Length],
                            Success = ok,
                            Duration = TimeSpan.FromMilliseconds(durationMs),
                            Timestamp = DateTimeOffset.UtcNow.AddSeconds(-((9 - i) * 30)),
                            Message = ok ? "Passed: HTTP 200" : "Failed: timeout after 30s",
                            Id = runId + "-" + i
                        };
                        at.Properties["batchTag"] = "AvailabilityTest-button";
                        at.Properties["runId"] = runId;
                        _ai.TrackAvailability(at);
                    }
                    _ai.Flush();
                });

                StatusPanel.Visible = true;
                StatusTitle.Text = "Synthetic Availability Test";
                StatusMessage.Text = "Recorded 10 availability results (8 success, 2 failure) across 5 regions over the last 5 minutes.";
            }
        }

        protected void ButtonHealthCheckFailing_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonHealthCheckFailing"))
            {
                var baseUrl = Request.Url.GetLeftPart(UriPartial.Authority);
                Task.Run(() =>
                {
                    System.Diagnostics.Trace.TraceWarning(
                        "[AppServiceScenarios] HealthCheckFailing: emitting 60 GET /health 503 records over 60s");
                    for (int i = 0; i < 60; i++)
                    {
                        EmitSyntheticRequest(baseUrl, "HealthCheck.aspx", 503, 35, i, "HealthCheckFailing-button");
                        Thread.Sleep(1000);
                    }
                    _ai.Flush();
                });

                StatusPanel.Visible = true;
                StatusTitle.Text = "Health Check Failing (Simulated)";
                StatusMessage.Text = "Recording 60 'GET /HealthCheck.aspx 503' rows in the background, 1 per second for 60 seconds. AutoHeal/health-check pattern.";
            }
        }

        protected void ButtonTls10Only_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonTls10Only", 500))
            {
                const string target = "tls-strict-endpoint.contoso.com";
                const string url = "https://" + target + "/api/orders";

                EmitSyntheticDependency(
                    type: "Http",
                    target: target,
                    name: "POST /api/orders",
                    data: url,
                    durationMs: 1500,
                    success: false,
                    resultCode: "0",
                    batchTag: "Tls10Only-button");

                var inner = new System.IO.IOException(
                    "Authentication failed because the remote party has closed the transport stream.");
                var ex = new System.Security.Authentication.AuthenticationException(
                    "A call to SSPI failed, see inner exception. The client and server cannot communicate, " +
                    "because they do not possess a common algorithm. (TLS 1.0 disabled by server policy.)",
                    inner);
                var et = new ExceptionTelemetry(ex);
                et.Properties["batchTag"] = "Tls10Only-button";
                et.Properties["scenario"] = "TLS 1.0 client calling TLS 1.2-only endpoint";
                et.Properties["target"] = target;
                _ai.TrackException(et);

                StatusPanel.Visible = true;
                StatusTitle.Text = "TLS 1.0 Handshake Failure";
                StatusMessage.Text = "Recorded a failed HTTPS dependency to '" + target + "' plus an AuthenticationException simulating a TLS 1.0 client rejected by a TLS 1.2-only endpoint.";
            }
        }

        protected void ButtonMissingAppSetting_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonMissingAppSetting", 500))
            {
                const string key = "AppServiceScenarios:RequiredEndpointUrl";
                string value = ConfigurationManager.AppSettings[key];
                Exception caught = null;
                try
                {
                    // Deliberate NRE — value is null because the key is not configured.
                    int len = value.Length;
                    System.Diagnostics.Trace.TraceInformation("[AppServiceScenarios] MissingAppSetting: len=" + len);
                }
                catch (NullReferenceException ex)
                {
                    caught = ex;
                    var et = new ExceptionTelemetry(ex);
                    et.Properties["batchTag"] = "MissingAppSetting-button";
                    et.Properties["scenario"] = "Missing app setting / null config value";
                    et.Properties["missingKey"] = key;
                    _ai.TrackException(et);
                }

                StatusPanel.Visible = true;
                StatusTitle.Text = "Missing App Setting";
                StatusMessage.Text = caught == null
                    ? "Unexpectedly: AppSettings['" + key + "'] returned a non-null value."
                    : "AppSettings['" + key + "'] was null → NullReferenceException recorded in App Insights.";
            }
        }

        // ============================================================
        // Load test: fire X concurrent HTTP GET requests against this site
        // ============================================================

        private sealed class LoadTestSummary
        {
            public int Total;
            public int Success;
            public int Fail;
            public long TotalMs;
            public double AvgMs;
            public long P50Ms;
            public long P95Ms;
            public long P99Ms;
            public long MaxMs;
            public Dictionary<int, int> StatusCounts = new Dictionary<int, int>();
        }

        protected void ButtonRunLoadTest_Click(object sender, EventArgs e)
        {
            using (TrackInlineButton("ButtonRunLoadTest"))
            {
                // Parse and clamp inputs.
                int count;
                if (!int.TryParse(LoadTestCount.Text, out count) || count < 1) count = 100;
                if (count > 5000) count = 5000;

                int concurrency;
                if (!int.TryParse(LoadTestConcurrency.Text, out concurrency) || concurrency < 1) concurrency = 50;
                if (concurrency > 200) concurrency = 200;
                if (concurrency > count) concurrency = count;

                string path = (LoadTestPath.Text ?? "").Trim();
                if (string.IsNullOrEmpty(path)) path = "/HealthCheck.aspx";
                if (!path.StartsWith("/")) path = "/" + path;

                var baseUrl = Request.Url.GetLeftPart(UriPartial.Authority);
                var fullUrl = baseUrl + path;
                var batchTag = "LoadTest-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");

                System.Diagnostics.Trace.TraceInformation(
                    "[AppServiceScenarios] LoadTest start: count={0}, concurrency={1}, target={2}, batchTag={3}",
                    count, concurrency, fullUrl, batchTag);

                // Run on a background task. Wait up to 85 seconds (under ASP.NET's
                // default executionTimeout of 110 s).
                var task = Task.Run(() => RunLoadTestAsync(fullUrl, count, concurrency, batchTag));
                bool completed = task.Wait(TimeSpan.FromSeconds(85));

                StatusPanel.Visible = true;
                if (completed && task.Exception == null)
                {
                    var s = task.Result;
                    double rps = s.TotalMs > 0 ? (s.Total * 1000.0 / s.TotalMs) : 0;
                    StatusTitle.Text = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "Load Test Completed \u2014 {0} requests \u00B7 concurrency {1} \u00B7 {2} ms \u00B7 {3:F1} req/s",
                        s.Total, concurrency, s.TotalMs, rps);
                    StatusMessage.Text = RenderLoadTestResult(s, fullUrl, concurrency, batchTag);
                }
                else
                {
                    StatusTitle.Text = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "Load Test Running in Background \u2014 {0} requests \u00B7 concurrency {1}",
                        count, concurrency);
                    StatusMessage.Text = RenderLoadTestPending(count, fullUrl, concurrency, batchTag);
                }
            }
        }

        private static string RenderLoadTestResult(LoadTestSummary s, string url, int concurrency, string batchTag)
        {
            double successPct = s.Total > 0 ? (s.Success * 100.0 / s.Total) : 0;
            double failPct = s.Total > 0 ? (s.Fail * 100.0 / s.Total) : 0;
            double rps = s.TotalMs > 0 ? (s.Total * 1000.0 / s.TotalMs) : 0;
            string failClass = s.Fail == 0 ? "lt-tile-success" : "lt-tile-fail";

            var sb = new System.Text.StringBuilder(2048);
            sb.Append("<div class=\"lt-meta-row\">");
            sb.Append("<span class=\"lt-result-label\">Target</span> <code class=\"lt-target-code\">")
              .Append(System.Web.HttpUtility.HtmlEncode(url))
              .Append("</code>");
            sb.Append(" <span class=\"lt-result-tag\">").Append(System.Web.HttpUtility.HtmlEncode(batchTag)).Append("</span>");
            sb.Append("</div>");

            sb.Append("<div class=\"lt-result-grid\">");
            AppendTile(sb, "lt-tile-success", "Success", s.Success.ToString(), string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F1}%", successPct));
            AppendTile(sb, failClass, "Failed", s.Fail.ToString(), string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F1}%", failPct));
            AppendTile(sb, "", "Avg latency", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F0} ms", s.AvgMs), null);
            AppendTile(sb, "", "P50", s.P50Ms + " ms", null);
            AppendTile(sb, "", "P95", s.P95Ms + " ms", null);
            AppendTile(sb, "", "P99", s.P99Ms + " ms", null);
            AppendTile(sb, "", "Max", s.MaxMs + " ms", null);
            AppendTile(sb, "", "Throughput", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F1}", rps), "req/s");
            sb.Append("</div>");

            sb.Append("<div class=\"lt-result-statuses\"><span class=\"lt-result-label\">Status codes</span><ul class=\"lt-status-list\">");
            var keys = new List<int>(s.StatusCounts.Keys);
            keys.Sort();
            foreach (var k in keys)
            {
                string pillClass;
                if (k >= 200 && k < 300) pillClass = "lt-status-2xx";
                else if (k >= 300 && k < 400) pillClass = "lt-status-3xx";
                else if (k >= 400 && k < 500) pillClass = "lt-status-4xx";
                else if (k >= 500) pillClass = "lt-status-5xx";
                else pillClass = "lt-status-err";
                string label = k == 0 ? "ERR" : k.ToString();
                sb.Append("<li class=\"lt-status-pill ").Append(pillClass).Append("\">")
                  .Append(label).Append(" <strong>&times;").Append(s.StatusCounts[k]).Append("</strong></li>");
            }
            sb.Append("</ul></div>");
            return sb.ToString();
        }

        private static void AppendTile(System.Text.StringBuilder sb, string extraClass, string label, string value, string sub)
        {
            sb.Append("<div class=\"lt-tile");
            if (!string.IsNullOrEmpty(extraClass)) sb.Append(' ').Append(extraClass);
            sb.Append("\">");
            sb.Append("<span class=\"lt-tile-label\">").Append(System.Web.HttpUtility.HtmlEncode(label)).Append("</span>");
            sb.Append("<span class=\"lt-tile-value\">").Append(System.Web.HttpUtility.HtmlEncode(value)).Append("</span>");
            if (!string.IsNullOrEmpty(sub))
            {
                sb.Append("<span class=\"lt-tile-sub\">").Append(System.Web.HttpUtility.HtmlEncode(sub)).Append("</span>");
            }
            sb.Append("</div>");
        }

        private static string RenderLoadTestPending(int count, string url, int concurrency, string batchTag)
        {
            var sb = new System.Text.StringBuilder(512);
            sb.Append("<div class=\"lt-meta-row\">");
            sb.Append("<span class=\"lt-result-label\">Target</span> <code class=\"lt-target-code\">")
              .Append(System.Web.HttpUtility.HtmlEncode(url)).Append("</code>");
            sb.Append(" <span class=\"lt-result-tag\">").Append(System.Web.HttpUtility.HtmlEncode(batchTag)).Append("</span>");
            sb.Append("</div>");
            sb.Append("<p class=\"lt-result-pending-note\">Results will surface in Application Insights. Sample query:</p>");
            sb.Append("<pre class=\"lt-result-kql\">customEvents\n| where name == \"AppServiceScenarios.LoadTestSummary\"\n| where tostring(customDimensions.batchTag) == \"")
              .Append(System.Web.HttpUtility.HtmlEncode(batchTag)).Append("\"</pre>");
            return sb.ToString();
        }

        private async Task<LoadTestSummary> RunLoadTestAsync(string url, int count, int concurrency, string batchTag)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var sem = new SemaphoreSlim(concurrency);
            var latencies = new long[count];
            var statuses = new int[count];
            int success = 0, fail = 0;

            var tasks = new Task[count];
            for (int i = 0; i < count; i++)
            {
                int idx = i;
                tasks[idx] = Task.Run(async () =>
                {
                    await sem.WaitAsync();
                    var rsw = System.Diagnostics.Stopwatch.StartNew();
                    int statusCode = 0;
                    bool ok = false;
                    try
                    {
                        using (var resp = await _loadTestHttp.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                        {
                            statusCode = (int)resp.StatusCode;
                            ok = resp.IsSuccessStatusCode;
                        }
                    }
                    catch (Exception)
                    {
                        ok = false;
                        statusCode = 0;
                    }
                    finally
                    {
                        rsw.Stop();
                        latencies[idx] = rsw.ElapsedMilliseconds;
                        statuses[idx] = statusCode;
                        if (ok) Interlocked.Increment(ref success);
                        else Interlocked.Increment(ref fail);
                        sem.Release();
                    }
                });
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            // Aggregate stats.
            var sorted = (long[])latencies.Clone();
            Array.Sort(sorted);
            long p50 = sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * 0.50))];
            long p95 = sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * 0.95))];
            long p99 = sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * 0.99))];
            long max = sorted[sorted.Length - 1];
            double avg = 0;
            for (int i = 0; i < count; i++) avg += latencies[i];
            avg /= count;

            var statusCounts = new Dictionary<int, int>();
            for (int i = 0; i < count; i++)
            {
                int s = statuses[i];
                if (statusCounts.ContainsKey(s)) statusCounts[s]++;
                else statusCounts[s] = 1;
            }

            // Emit aggregate event.
            var statusKeys = new List<int>(statusCounts.Keys);
            statusKeys.Sort();
            var statusParts = new List<string>();
            foreach (var k in statusKeys) statusParts.Add(k + "=" + statusCounts[k]);

            var ev = new EventTelemetry("AppServiceScenarios.LoadTestSummary");
            ev.Properties["batchTag"] = batchTag;
            ev.Properties["target"] = url;
            ev.Properties["concurrency"] = concurrency.ToString();
            ev.Properties["statusCounts"] = string.Join(",", statusParts);
            ev.Metrics["count"] = count;
            ev.Metrics["success"] = success;
            ev.Metrics["fail"] = fail;
            ev.Metrics["totalMs"] = sw.ElapsedMilliseconds;
            ev.Metrics["avgMs"] = avg;
            ev.Metrics["p50Ms"] = p50;
            ev.Metrics["p95Ms"] = p95;
            ev.Metrics["p99Ms"] = p99;
            ev.Metrics["maxMs"] = max;
            _ai.TrackEvent(ev);
            _ai.GetMetric("AppServiceScenarios.LoadTestRequestCount").TrackValue(count);
            _ai.Flush();

            System.Diagnostics.Trace.TraceInformation(
                "[AppServiceScenarios] LoadTest done: count={0}, success={1}, fail={2}, totalMs={3}, avgMs={4:F0}, p95Ms={5}, batchTag={6}",
                count, success, fail, sw.ElapsedMilliseconds, avg, p95, batchTag);

            return new LoadTestSummary
            {
                Total = count,
                Success = success,
                Fail = fail,
                TotalMs = sw.ElapsedMilliseconds,
                AvgMs = avg,
                P50Ms = p50,
                P95Ms = p95,
                P99Ms = p99,
                MaxMs = max,
                StatusCounts = statusCounts
            };
        }
    }
}
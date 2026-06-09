<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="AppServiceScenarios.Default" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <title>Azure App Service - Performance Testing Suite</title>
    <link rel="stylesheet" type="text/css" href="styles.css?v=20260506" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="" />
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet" />
</head>
<body>
    <form id="form1" runat="server">
        <!-- Navigation Bar -->
        <nav class="navbar">
            <div class="nav-container">
                <div class="nav-brand">
                    <span class="brand-icon">⚡</span>
                    <span class="brand-text">Performance Suite</span>
                </div>
            </div>
        </nav>

        <!-- Hero Section -->
        <section class="hero">
            <div class="hero-content">
                <h1 class="hero-title">Performance Testing & Diagnostics Platform</h1>
                <p class="hero-subtitle">Simulate real-world scenarios to validate application resilience and optimize Azure App Service performance.</p>
            </div>
        </section>

        <!-- Main Content -->
        <main class="main-content">
            <div class="content-wrapper">
                <!-- Status Message -->
                <asp:Panel ID="StatusPanel" runat="server" Visible="false" CssClass="status-panel">
                    <div class="status-content">
                        <div class="status-icon">✓</div>
                        <div class="status-message">
                            <h3><asp:Label ID="StatusTitle" runat="server" /></h3>
                            <div class="status-text"><asp:Literal ID="StatusMessage" runat="server" Mode="PassThrough" /></div>
                        </div>
                        <asp:Button ID="BackButton" runat="server" Text="Go Back" OnClick="BackButton_Click" CssClass="back-button" />
                    </div>
                </asp:Panel>

                <!-- Tab Navigation -->
                <div class="tab-container">
                    <button type="button" class="tab-button active" onclick="switchTab(event, 'critical')">Critical Tests</button>
                    <button type="button" class="tab-button" onclick="switchTab(event, 'delay')">Response Delays</button>
                    <button type="button" class="tab-button" onclick="switchTab(event, 'fastresponse')">Fast Response</button>
                    <button type="button" class="tab-button" onclick="switchTab(event, 'http4xx')">HTTP 4xx</button>
                    <button type="button" class="tab-button" onclick="switchTab(event, 'http5xx')">HTTP 5xx</button>
                    <button type="button" class="tab-button" onclick="switchTab(event, 'advanced')">Advanced</button>
                    <button type="button" class="tab-button" onclick="switchTab(event, 'appservice')">App Service</button>
                    <button type="button" class="tab-button" onclick="switchTab(event, 'customer')">Customer Scenarios</button>
                    <button type="button" class="tab-button" onclick="switchTab(event, 'loadtest')">Load Test</button>
                </div>

                <!-- Critical Tests Tab -->
                <div id="critical" class="tab-content active">
                    <div class="test-section">
                        <div class="section-header">
                            <h2>Critical Performance Tests</h2>
                            <p>Execute tests that simulate critical application failures and resource exhaustion</p>
                        </div>
                        <div class="test-grid">
                            <div class="test-card">
                                <div class="card-icon crash">💥</div>
                                <h3>Application Crash</h3>
                                <p>Triggers a stack overflow exception</p>
                                <asp:Button ID="Button1" runat="server" Text="Execute Test" OnClick="Button1_Click" CssClass="test-button danger" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon cpu">⚙️</div>
                                <h3>High CPU Load</h3>
                                <p>Generates sustained 100% CPU usage</p>
                                <asp:Button ID="Button2" runat="server" Text="Execute Test" OnClick="Button2_Click" CssClass="test-button danger" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon memory">💾</div>
                                <h3>Memory Exhaustion</h3>
                                <p>Rapidly consumes available memory</p>
                                <asp:Button ID="Button3" runat="server" Text="Execute Test" OnClick="Button3_Click" CssClass="test-button danger" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon crash">🧨</div>
                                <h3>NullReferenceException</h3>
                                <p>Dedicated page that throws an unhandled NRE on GET. Surfaces as HTTP 500 + App Insights ExceptionTelemetry.</p>
                                <a href="NullRef.aspx" class="test-button danger" target="_blank">Open NullRef.aspx</a>
                            </div>
                            <div class="test-card">
                                <div class="card-icon crash">☠️</div>
                                <h3>StackOverflow → w3wp Crash</h3>
                                <p>Dedicated page; <code>?go=1</code> recurses infinitely and crashes the w3wp worker process. Kills all in-flight requests on that instance.</p>
                                <a href="StackOverflow.aspx" class="test-button danger" target="_blank">Open StackOverflow.aspx</a>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Response Delays Tab -->
                <div id="delay" class="tab-content">
                    <div class="test-section">
                        <div class="section-header">
                            <h2>Response Delay Tests</h2>
                            <p>Simulate slow response times to test timeout handling and user experience</p>
                        </div>
                        <div class="test-grid">
                            <div class="test-card">
                                <div class="card-icon delay">⏱️</div>
                                <h3>10 Second Delay</h3>
                                <p>Simulates a moderate response delay</p>
                                <asp:Button ID="ButtonSlow10" runat="server" Text="Execute Test" OnClick="ButtonSlow10_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon delay">⏰</div>
                                <h3>60 Second Delay</h3>
                                <p>Simulates an extended response delay</p>
                                <asp:Button ID="ButtonSlow60" runat="server" Text="Execute Test" OnClick="ButtonSlow60_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon delay">🔄</div>
                                <h3>100x 10 Second Delays</h3>
                                <p>Generates 100 slow requests (10s each) in background</p>
                                <asp:Button ID="ButtonSlow10x100" runat="server" Text="Execute Test" OnClick="ButtonSlow10x100_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon delay">🔁</div>
                                <h3>100x 60 Second Delays</h3>
                                <p>Generates 100 slow requests (60s each) in background</p>
                                <asp:Button ID="ButtonSlow60x100" runat="server" Text="Execute Test" OnClick="ButtonSlow60x100_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon delay">⚡</div>
                                <h3>100x 10s Parallel (Real HTTP → triggers Auto-Heal)</h3>
                                <p>Fires 100 real parallel HTTP GETs against /Slow10.aspx. Each takes ~10s. Hits the auto-heal slow-request rule (10 reqs &gt;7s in 60s).</p>
                                <asp:Button ID="ButtonSlow10ParallelReal" runat="server" Text="Execute Test" OnClick="ButtonSlow10ParallelReal_Click" CssClass="test-button" />
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Fast Response Tab -->
                <div id="fastresponse" class="tab-content">
                    <div class="test-section">
                        <div class="section-header">
                            <h2>Fast Response Test</h2>
                            <p>Test optimal performance with instant response times</p>
                        </div>
                        <div class="test-grid">
                            <div class="test-card">
                                <div class="card-icon delay">⚡</div>
                                <h3>Fast Response</h3>
                                <p>Optimal performance - instant response</p>
                                <a href="FastResponse.aspx" class="test-button" style="text-decoration: none;">Execute Test</a>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- HTTP 4xx Tab -->
                <div id="http4xx" class="tab-content">
                    <div class="test-section">
                        <div class="section-header">
                            <h2>HTTP 4xx Client Error Tests</h2>
                            <p>Generate HTTP 4xx client errors for testing error handling and monitoring</p>
                        </div>
                        <div class="test-grid">
                            <div class="test-card">
                                <div class="card-icon error">⚠️</div>
                                <h3>HTTP 400 Bad Request</h3>
                                <p>Generates 100 HTTP 400 errors</p>
                                <asp:Button ID="Button400" runat="server" Text="Execute Test" OnClick="Button400_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">🔒</div>
                                <h3>HTTP 401 Unauthorized</h3>
                                <p>Generates 100 HTTP 401 errors</p>
                                <asp:Button ID="Button401" runat="server" Text="Execute Test" OnClick="Button401_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">🚫</div>
                                <h3>HTTP 403 Forbidden</h3>
                                <p>Generates 100 HTTP 403 errors</p>
                                <asp:Button ID="Button403" runat="server" Text="Execute Test" OnClick="Button403_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">❓</div>
                                <h3>HTTP 404 Not Found</h3>
                                <p>Generates 100 HTTP 404 errors</p>
                                <asp:Button ID="Button404" runat="server" Text="Execute Test" OnClick="Button404_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">🐢</div>
                                <h3>HTTP 429 Too Many Requests</h3>
                                <p>Generates 100 HTTP 429 throttling errors</p>
                                <asp:Button ID="Button429" runat="server" Text="Execute Test" OnClick="Button429_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">⏲️</div>
                                <h3>HTTP 408 Request Timeout</h3>
                                <p>Generates 100 HTTP 408 request-timeout responses</p>
                                <asp:Button ID="Button408" runat="server" Text="Execute Test" OnClick="Button408_Click" CssClass="test-button" />
                            </div>
                        </div>
                    </div>
                </div>

                <!-- HTTP 5xx Tab -->
                <div id="http5xx" class="tab-content">
                    <div class="test-section">
                        <div class="section-header">
                            <h2>HTTP 5xx Server Error Tests</h2>
                            <p>Generate server-side HTTP 5xx errors to validate error handling, alerting, and Application Insights instrumentation</p>
                        </div>
                        <div class="test-grid">
                            <div class="test-card">
                                <div class="card-icon error">❌</div>
                                <h3>HTTP 500 (in-process)</h3>
                                <p>Generates 100 HTTP 500 responses via background calls to Http500.aspx</p>
                                <asp:Button ID="Button4" runat="server" Text="Execute Test" OnClick="Button4_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">⚡</div>
                                <h3>100x HTTP 500 Parallel (Real HTTP → triggers Auto-Heal)</h3>
                                <p>Fires 100 real parallel HTTP GETs against /Http500.aspx. Hits the auto-heal status-code rule (10 reqs HTTP 500 in 60s).</p>
                                <asp:Button ID="ButtonHttp500ParallelReal" runat="server" Text="Execute Test" OnClick="ButtonHttp500ParallelReal_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">📄</div>
                                <h3>HTTP 500 Error Page</h3>
                                <p>Navigate to the custom HTTP 500 error page</p>
                                <a href="Http500.aspx" class="test-button" style="text-decoration: none;">View Error Page</a>
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">🛑</div>
                                <h3>HTTP 502 Bad Gateway</h3>
                                <p>Generates 100 HTTP 502 responses</p>
                                <asp:Button ID="Button502" runat="server" Text="Execute Test" OnClick="Button502_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">🚧</div>
                                <h3>HTTP 503 Service Unavailable</h3>
                                <p>Generates 100 HTTP 503 responses</p>
                                <asp:Button ID="Button503" runat="server" Text="Execute Test" OnClick="Button503_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">⌛</div>
                                <h3>HTTP 504 Gateway Timeout</h3>
                                <p>Generates 100 HTTP 504 responses</p>
                                <asp:Button ID="Button504" runat="server" Text="Execute Test" OnClick="Button504_Click" CssClass="test-button" />
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Advanced Scenarios Tab -->
                <div id="advanced" class="tab-content">
                    <div class="test-section">
                        <div class="section-header">
                            <h2>Advanced Performance Scenarios</h2>
                            <p>Reproduce advanced failure modes commonly seen on Azure App Service: thread pool starvation, deadlocks, gradual leaks, large payloads, hung dependencies, disk pressure, and hard process crashes.</p>
                        </div>
                        <div class="test-grid">
                            <div class="test-card">
                                <div class="card-icon cpu">🧵</div>
                                <h3>ThreadPool Starvation</h3>
                                <p>Queues 200 blocking sync sleeps to exhaust the worker threadpool</p>
                                <asp:Button ID="ButtonThreadPoolStarve" runat="server" Text="Execute Test" OnClick="ButtonThreadPoolStarve_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon crash">🔒</div>
                                <h3>Sync-over-Async Deadlock</h3>
                                <p>Triggers a classic .Result deadlock on the request context</p>
                                <asp:Button ID="ButtonDeadlock" runat="server" Text="Execute Test" OnClick="ButtonDeadlock_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon memory">💧</div>
                                <h3>Gradual Memory Leak</h3>
                                <p>Adds 100 MB into a static cache (cumulative across calls)</p>
                                <asp:Button ID="ButtonMemoryLeak" runat="server" Text="Execute Test" OnClick="ButtonMemoryLeak_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon delay">📦</div>
                                <h3>Large Response Payload</h3>
                                <p>Returns a ~10 MB response to test bandwidth and timeouts</p>
                                <asp:Button ID="ButtonLargePayload" runat="server" Text="Execute Test" OnClick="ButtonLargePayload_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon delay">🌐</div>
                                <h3>Outbound Dependency Hang</h3>
                                <p>Calls an unreachable host with a 30s timeout (simulates hung downstream)</p>
                                <asp:Button ID="ButtonOutboundHang" runat="server" Text="Execute Test" OnClick="ButtonOutboundHang_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon memory">💽</div>
                                <h3>Disk I/O Pressure</h3>
                                <p>Writes 10 × 50 MB files to %TEMP% (auto-cleans afterwards)</p>
                                <asp:Button ID="ButtonDiskIO" runat="server" Text="Execute Test" OnClick="ButtonDiskIO_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon crash">☠️</div>
                                <h3>Hard Process Crash (FailFast)</h3>
                                <p>Terminates the worker process via Environment.FailFast</p>
                                <asp:Button ID="ButtonFailFast" runat="server" Text="Execute Test" OnClick="ButtonFailFast_Click" CssClass="test-button danger" />
                            </div>
                        </div>
                    </div>
                </div>

                <!-- App Service Specific Tab -->
                <div id="appservice" class="tab-content">
                    <div class="test-section">
                        <div class="section-header">
                            <h2>App Service-Specific Failures</h2>
                            <p>Reproduce failure modes specifically tied to Azure App Service: Health Check / AutoHeal triggers, worker recycle, port exhaustion, and Gen2 GC pressure.</p>
                        </div>
                        <div class="test-grid">
                            <div class="test-card">
                                <div class="card-icon ok">❤️</div>
                                <h3>Health Check Endpoint</h3>
                                <p>Configurable endpoint for App Service Health Check / AutoHeal. Supports <code>?status=503</code> and <code>?fail=N</code>.</p>
                                <a href="HealthCheck.aspx" class="test-button" style="text-decoration: none;">Open Endpoint</a>
                            </div>
                            <div class="test-card">
                                <div class="card-icon ok">💚</div>
                                <h3>Health Check — Always Unhealthy</h3>
                                <p>Quick link returning HTTP 503 every time</p>
                                <a href="HealthCheck.aspx?status=503" class="test-button secondary" style="text-decoration: none;">Open ?status=503</a>
                            </div>
                            <div class="test-card">
                                <div class="card-icon ok">💛</div>
                                <h3>Health Check — Intermittent</h3>
                                <p>Quick link failing every 3rd request</p>
                                <a href="HealthCheck.aspx?fail=3" class="test-button secondary" style="text-decoration: none;">Open ?fail=3</a>
                            </div>
                            <div class="test-card">
                                <div class="card-icon crash">🪦</div>
                                <h3>Background Thread Crash</h3>
                                <p>Throws an unhandled exception on a background thread (recycles the w3wp worker on .NET 4.x)</p>
                                <asp:Button ID="ButtonBgCrash" runat="server" Text="Execute Test" OnClick="ButtonBgCrash_Click" CssClass="test-button danger" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon app">🔌</div>
                                <h3>HttpClient Socket Exhaustion</h3>
                                <p>Creates 200 short-lived HttpClient instances to ports in TIME_WAIT — classic ephemeral-port exhaustion pattern</p>
                                <asp:Button ID="ButtonSocketExhaust" runat="server" Text="Execute Test" OnClick="ButtonSocketExhaust_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon memory">📊</div>
                                <h3>LOH / Gen2 GC Pressure</h3>
                                <p>Allocates 200 × 1 MB byte[] (Large Object Heap) to trigger Gen2 collections and visible GC pauses</p>
                                <asp:Button ID="ButtonLOH" runat="server" Text="Execute Test" OnClick="ButtonLOH_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon delay">📥</div>
                                <h3>Slow Page Load (Throttled Stream)</h3>
                                <p>Streams 1 MB of response over 30 seconds — tests Always On / front-end timeouts</p>
                                <asp:Button ID="ButtonThrottled" runat="server" Text="Execute Test" OnClick="ButtonThrottled_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">📋</div>
                                <h3>Mixed Error Burst</h3>
                                <p>Generates 25 each of 500, 502, 503, 504 in the background — simulates an unstable backend</p>
                                <asp:Button ID="ButtonMixedBurst" runat="server" Text="Execute Test" OnClick="ButtonMixedBurst_Click" CssClass="test-button" />
                            </div>                        </div>
                    </div>
                </div>

                <!-- Customer Scenarios Tab -->
                <div id="customer" class="tab-content">
                    <div class="test-section">
                        <div class="section-header">
                            <h2>Customer-Pattern Failures</h2>
                            <p>Common production failure shapes seen in App Service support tickets — DNS, SQL, Redis, TLS, configuration, identity. Each button writes the matching telemetry to Application Insights so you can practice queries and dashboards.</p>
                        </div>
                        <div class="test-grid">
                            <div class="test-card">
                                <div class="card-icon app">🌐</div>
                                <h3>DNS Resolution Failure</h3>
                                <p>Resolves a non-existent hostname; emits a failed dependency + SocketException to AI</p>
                                <asp:Button ID="ButtonDNSFail" runat="server" Text="Execute Test" OnClick="ButtonDNSFail_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">🗄️</div>
                                <h3>SQL Connection Pool Exhaustion</h3>
                                <p>Records 30 SQL pool-exhaustion dependencies + InvalidOperationException records</p>
                                <asp:Button ID="ButtonSqlConnectionPoolExhaust" runat="server" Text="Execute Test" OnClick="ButtonSqlConnectionPoolExhaust_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon delay">🐢</div>
                                <h3>SQL Slow Query</h3>
                                <p>Records a 35-second SQL dependency simulating a missing-index full-scan</p>
                                <asp:Button ID="ButtonSqlSlowQuery" runat="server" Text="Execute Test" OnClick="ButtonSqlSlowQuery_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">⚡</div>
                                <h3>Redis Timeout</h3>
                                <p>Records 5 Redis GET timeouts (5s each) plus matching TimeoutException records</p>
                                <asp:Button ID="ButtonRedisTimeout" runat="server" Text="Execute Test" OnClick="ButtonRedisTimeout_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon ok">🆔</div>
                                <h3>Instance Identity</h3>
                                <p>Reports WEBSITE_INSTANCE_ID, machine name, PID, region, SKU, CLR version</p>
                                <asp:Button ID="ButtonInstanceIdentity" runat="server" Text="Execute Test" OnClick="ButtonInstanceIdentity_Click" CssClass="test-button secondary" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon app">📊</div>
                                <h3>Custom Event Burst</h3>
                                <p>Emits 1000 'AppServiceScenarios.UserAction' custom events with random userId/action</p>
                                <asp:Button ID="ButtonCustomEventBurst" runat="server" Text="Execute Test" OnClick="ButtonCustomEventBurst_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon ok">🟢</div>
                                <h3>Synthetic Availability Test</h3>
                                <p>Records 10 availabilityResults rows (8 success, 2 failure) across 5 regions</p>
                                <asp:Button ID="ButtonAvailabilityTestEndpoint" runat="server" Text="Execute Test" OnClick="ButtonAvailabilityTestEndpoint_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">💔</div>
                                <h3>Health Check Failing</h3>
                                <p>Records 60 'GET /HealthCheck.aspx 503' rows over 60 seconds (AutoHeal pattern)</p>
                                <asp:Button ID="ButtonHealthCheckFailing" runat="server" Text="Execute Test" OnClick="ButtonHealthCheckFailing_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">🔒</div>
                                <h3>TLS 1.0 Handshake Failure</h3>
                                <p>Records a failed HTTPS dependency + AuthenticationException for a TLS 1.0 client rejected by a TLS 1.2-only endpoint</p>
                                <asp:Button ID="ButtonTls10Only" runat="server" Text="Execute Test" OnClick="ButtonTls10Only_Click" CssClass="test-button" />
                            </div>
                            <div class="test-card">
                                <div class="card-icon crash">⚙️</div>
                                <h3>Missing App Setting</h3>
                                <p>Reads a non-existent appSetting and dereferences null → NullReferenceException recorded in AI</p>
                                <asp:Button ID="ButtonMissingAppSetting" runat="server" Text="Execute Test" OnClick="ButtonMissingAppSetting_Click" CssClass="test-button" />
                            </div>                        </div>
                    </div>
                </div>

                <!-- Load Test Tab -->
                <div id="loadtest" class="tab-content">
                    <div class="test-section">
                        <div class="section-header">
                            <h2>Site Load Test</h2>
                            <p>Send a configurable number of HTTP GET requests to a path on this site (loopback). Each call is a real round-trip captured by the codeless AI agent as a <code>RequestTelemetry</code> row. A summary <code>customEvents</code> row named <code>AppServiceScenarios.LoadTestSummary</code> is also emitted with count, success/fail, total/avg/p50/p95/p99/max latency.</p>
                        </div>
                        <div class="test-grid">
                            <div class="test-card" style="grid-column: span 2;">
                                <div class="card-icon app">🚀</div>
                                <h3>Run Load Test</h3>
                                <p>Capped at 5000 requests / 200 concurrent. Runs synchronously up to ~85 s; longer runs continue in the background and surface in App Insights with the same <code>batchTag</code>.</p>
                                <div class="loadtest-form">
                                    <label class="loadtest-field">
                                        <span>Request count</span>
                                        <asp:TextBox ID="LoadTestCount" runat="server" Text="100" CssClass="loadtest-input" />
                                    </label>
                                    <label class="loadtest-field">
                                        <span>Concurrency</span>
                                        <asp:TextBox ID="LoadTestConcurrency" runat="server" Text="50" CssClass="loadtest-input" />
                                    </label>
                                    <label class="loadtest-field loadtest-field-wide">
                                        <span>Target path</span>
                                        <asp:TextBox ID="LoadTestPath" runat="server" Text="/HealthCheck.aspx" CssClass="loadtest-input" />
                                    </label>
                                </div>
                                <asp:Button ID="ButtonRunLoadTest" runat="server" Text="Run Load Test" OnClick="ButtonRunLoadTest_Click" CssClass="test-button" />
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </main>

        <!-- Footer -->
        <footer class="footer">
            <p>⚠️ For development and testing purposes only. Do not run these tests in production environments.</p>
        </footer>

        <script>
            function switchTab(evt, tabName) {
                // Hide all tab contents
                var tabContents = document.getElementsByClassName('tab-content');
                for (var i = 0; i < tabContents.length; i++) {
                    tabContents[i].classList.remove('active');
                }
                
                // Remove active class from all buttons
                var tabButtons = document.getElementsByClassName('tab-button');
                for (var i = 0; i < tabButtons.length; i++) {
                    tabButtons[i].classList.remove('active');
                }
                
                // Show selected tab and mark button as active
                document.getElementById(tabName).classList.add('active');
                (evt && evt.currentTarget ? evt.currentTarget : evt.target).classList.add('active');
            }
        </script>
    </form>
</body>
</html>
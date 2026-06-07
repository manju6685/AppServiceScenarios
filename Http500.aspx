<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Http500.aspx.cs" Inherits="AppServiceScenarios.Http500" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <title>HTTP 500 - Server Error</title>
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
                <a href="Default.aspx" class="nav-button">← Back to Dashboard</a>
            </div>
        </nav>

        <!-- Hero Section -->
        <section class="hero">
            <div class="hero-content">
                <h1 class="hero-title">HTTP 500 Error Generated</h1>
                <p class="hero-subtitle">Server error simulation completed successfully for testing purposes.</p>
            </div>
        </section>

        <!-- Main Content -->
        <main class="main-content">
            <div class="content-wrapper">
                <div class="tab-content active">
                    <div class="test-section">
                        <div class="error-display">
                            <div class="error-icon-large">⚠️</div>
                            <div class="error-code">500</div>
                            <h2>Internal Server Error</h2>
                            <p class="error-description">
                                The server encountered an unexpected condition that prevented it from fulfilling the request.
                            </p>
                        </div>

                        <div class="test-grid" style="margin-top: 3rem;">
                            <div class="test-card">
                                <div class="card-icon error">✅</div>
                                <h3>Test Status</h3>
                                <p>HTTP 500 error successfully generated</p>
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">🔢</div>
                                <h3>Simulated Errors</h3>
                                <p>100 exceptions caught internally</p>
                            </div>
                            <div class="test-card">
                                <div class="card-icon error">🎯</div>
                                <h3>Purpose</h3>
                                <p>Performance testing and error handling validation</p>
                            </div>
                        </div>

                        <div style="text-align: center; margin-top: 3rem;">
                            <a href="Default.aspx" class="test-button" style="max-width: 300px; margin: 0 auto; display: block;">
                                Return to Dashboard
                            </a>
                        </div>
                    </div>
                </div>
            </div>
        </main>

        <!-- Footer -->
        <footer class="footer">
            <p>⚠️ For development and testing purposes only. Do not run these tests in production environments.</p>
        </footer>
    </form>

    <style>
        .error-display {
            text-align: center;
            padding: 2rem;
            background: rgba(26, 29, 41, 0.6);
            border: 1px solid rgba(239, 68, 68, 0.3);
            border-radius: 16px;
            margin-bottom: 2rem;
        }
        .error-icon-large {
            font-size: 5rem;
            margin-bottom: 1rem;
        }
        .error-code {
            font-size: 4rem;
            font-weight: 700;
            color: #ef4444;
            margin-bottom: 1rem;
        }
        .error-display h2 {
            color: #ffffff;
            font-size: 2rem;
            margin-bottom: 1rem;
        }
        .error-description {
            color: #a8b0c1;
            font-size: 1.1rem;
            line-height: 1.6;
            max-width: 600px;
            margin: 0 auto;
        }
    </style>
</body>
</html>

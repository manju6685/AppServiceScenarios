<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="HealthCheck.aspx.cs" Inherits="AppServiceScenarios.HealthCheck" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <title>Health Check Endpoint</title>
    <link rel="stylesheet" type="text/css" href="styles.css?v=20260506" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
</head>
<body>
    <form id="form1" runat="server">
        <div class="container">
            <h1>App Service Health Check Endpoint</h1>
            <asp:Label ID="lblMessage" runat="server" CssClass="message-label" />
            <p style="font-size:13px;color:var(--text-secondary);margin-top:8px;">
                Use this endpoint as the <strong>Health check path</strong> in App Service.<br />
                Query string: <code>?status=200</code> | <code>?status=503</code> | <code>?status=500</code> (default 200)<br />
                For an intermittent unhealthy endpoint, use <code>?fail=N</code> to fail every Nth request.
            </p>
            <p style="margin-top:16px;">
                <a href="Default.aspx" class="test-button secondary" style="display:inline-block;width:auto;">Back to Dashboard</a>
            </p>
        </div>
    </form>
</body>
</html>

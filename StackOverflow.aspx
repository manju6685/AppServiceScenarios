<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="StackOverflow.aspx.cs" Inherits="AppServiceScenarios.StackOverflowPage" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <title>StackOverflow - w3wp Crash</title>
    <link rel="stylesheet" type="text/css" href="styles.css?v=20260607" />
</head>
<body>
    <form id="form1" runat="server">
        <div class="container">
            <h1>StackOverflowException Scenario</h1>
            <p>
                This page deliberately overflows the call stack on the request thread.
                A <code>StackOverflowException</code> in .NET Framework is <strong>not catchable</strong>
                from user code: the CLR terminates the <code>w3wp.exe</code> worker process.
            </p>
            <p>
                Effects on Azure App Service:
            </p>
            <ul>
                <li>The current request returns no response (connection reset / 502).</li>
                <li>All other in-flight requests on the same worker also fail.</li>
                <li>App Service auto-restarts the worker; cold-start latency follows.</li>
                <li>Crash is visible in Event Viewer / Application Insights &quot;Availability dropped&quot; alerts.</li>
            </ul>
            <p class="warning">
                <strong>Trigger:</strong>
                <a href="StackOverflow.aspx?go=1">Click here to crash w3wp</a>
            </p>
            <asp:Label ID="lblMessage" runat="server" CssClass="message-label" />
        </div>
    </form>
</body>
</html>

<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="FastResponse.aspx.cs" Inherits="AppServiceScenarios.FastResponse" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <title>Fast Response</title>
    <link rel="stylesheet" type="text/css" href="styles.css?v=20260506" />
</head>
<body>
    <form id="form1" runat="server">
        <div class="container">
            <h1>Fast Response - Optimal Performance</h1>
            <asp:Label ID="lblMessage" runat="server" CssClass="message-label" />
            <br /><br />
            <a href="Default.aspx" class="test-button" style="text-decoration: none; display: inline-block;">Go Back to Home Page</a>
        </div>
    </form>
</body>
</html>

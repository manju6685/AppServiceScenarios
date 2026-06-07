<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Slow10.aspx.cs" Inherits="AppServiceScenarios.Slow10" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <title>Slow 10 Seconds</title>
    <link rel="stylesheet" type="text/css" href="styles.css?v=20260506" />
</head>
<body>
    <form id="form1" runat="server">
        <div class="container">
            <h1>Slow Page - 10 Seconds</h1>
            <asp:Label ID="lblMessage" runat="server" CssClass="message-label" />
        </div>
    </form>
</body>
</html>

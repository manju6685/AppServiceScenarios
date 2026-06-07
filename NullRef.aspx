<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="NullRef.aspx.cs" Inherits="AppServiceScenarios.NullRef" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <title>NullReferenceException</title>
    <link rel="stylesheet" type="text/css" href="styles.css?v=20260607" />
</head>
<body>
    <form id="form1" runat="server">
        <div class="container">
            <h1>NullReferenceException Scenario</h1>
            <asp:Label ID="lblMessage" runat="server" CssClass="message-label" />
        </div>
    </form>
</body>
</html>

using System;
using System.Net;
using System.Web;

namespace AppServiceScenarios
{
    public class Global : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            // Ensure outbound HTTPS calls (the background self-callback loops in
            // Default.aspx.cs) negotiate TLS 1.2. Azure App Service rejects older
            // protocols, which would cause every WebRequest to throw before any
            // request reaches Http500.aspx / Slow10.aspx — and therefore nothing
            // would show up in Application Insights.
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            // Allow more concurrent outbound connections per host (default is 2).
            ServicePointManager.DefaultConnectionLimit = 100;
        }
    }
}

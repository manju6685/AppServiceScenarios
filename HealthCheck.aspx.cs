using System;
using System.Threading;

namespace AppServiceScenarios
{
    public partial class HealthCheck : System.Web.UI.Page
    {
        private static int _requestCounter;

        protected void Page_Load(object sender, EventArgs e)
        {
            int statusCode = 200;

            // ?status=503 — return that status code for every request
            var statusParam = Request.QueryString["status"];
            if (!string.IsNullOrEmpty(statusParam) && int.TryParse(statusParam, out int parsed))
            {
                statusCode = parsed;
            }

            // ?fail=3 — return 503 every Nth request, 200 otherwise (intermittent failure)
            var failParam = Request.QueryString["fail"];
            if (!string.IsNullOrEmpty(failParam) && int.TryParse(failParam, out int fail) && fail > 0)
            {
                int n = Interlocked.Increment(ref _requestCounter);
                statusCode = (n % fail == 0) ? 503 : 200;
            }

            Response.StatusCode = statusCode;
            Response.StatusDescription = statusCode == 200 ? "OK" : "Unhealthy";
            Response.AddHeader("X-Health-Check-Endpoint", "AppServiceScenarios");

            lblMessage.Text = $"Health check responding with HTTP {statusCode} at {DateTime.UtcNow:o}";
        }
    }
}

using System;

namespace AppServiceScenarios
{
    /// <summary>
    /// Deliberately throws an unhandled NullReferenceException on every GET so the
    /// failure surfaces as:
    ///   * HTTP 500 from IIS
    ///   * ExceptionTelemetry in Application Insights (auto-captured by the codeless agent)
    ///   * "NullReferenceException" entry in the Failures blade
    ///
    /// Demo flow:
    ///   GET /NullRef.aspx                  -> throws (default)
    ///   GET /NullRef.aspx?safe=1           -> returns 200 with explanation, no throw
    ///
    /// Used by the App Service training pack for "App code throws unhandled exception".
    /// </summary>
    public partial class NullRef : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (string.Equals(Request.QueryString["safe"], "1", StringComparison.Ordinal))
            {
                lblMessage.Text =
                    "Safe mode: no exception thrown. Remove ?safe=1 to trigger an unhandled " +
                    "NullReferenceException that App Insights will record in the Failures blade.";
                return;
            }

            // Simulate a common real-world pattern: a config value that was expected to be
            // present comes back null, and code dereferences it without a guard.
            string config = null;
            // Unhandled — let it bubble up to IIS so the platform records HTTP 500.
            int length = config.Length;
            lblMessage.Text = "Unreachable: length=" + length;
        }
    }
}

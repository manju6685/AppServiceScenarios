using System;
using System.Diagnostics;

namespace AppServiceScenarios
{
    /// <summary>
    /// Triggers a StackOverflowException to crash the w3wp.exe worker process.
    ///
    /// Important: a StackOverflowException in .NET Framework is uncatchable from
    /// user code (since CLR 2.0). It tears down the entire AppDomain / process.
    /// Azure App Service will restart the worker; expect a brief outage on this
    /// instance and a "Process crashed" event in the platform diagnostics.
    ///
    /// Demo flow:
    ///   GET /StackOverflow.aspx           -> landing page with warning + trigger link
    ///   GET /StackOverflow.aspx?go=1      -> recurses infinitely, kills w3wp
    ///
    /// Class is named StackOverflowPage so it does not collide with
    /// System.StackOverflowException via short name.
    /// </summary>
    public partial class StackOverflowPage : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!string.Equals(Request.QueryString["go"], "1", StringComparison.Ordinal))
            {
                lblMessage.Text = "Safe mode: click the trigger link above to crash w3wp.";
                return;
            }

            // Emit a breadcrumb so post-mortem can correlate the crash event in
            // Event Viewer / Application Insights with the originating request.
            // Fully-qualify Trace because System.Web.UI.Page.Trace shadows the name.
            System.Diagnostics.Trace.TraceError(
                "[AppServiceScenarios] StackOverflow.aspx?go=1 about to overflow the stack " +
                "on w3wp PID " + Process.GetCurrentProcess().Id);

            Recurse(0);

            // Unreachable.
            lblMessage.Text = "Unreachable";
        }

        private static long Recurse(long depth)
        {
            // Unbounded recursion. JIT cannot tail-call optimize this because the
            // return value is used, so the stack grows by one frame per call and
            // overflows in roughly tens of thousands of frames on x86.
            return Recurse(depth + 1) + 1;
        }
    }
}

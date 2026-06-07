using System;

namespace AppServiceScenarios
{
    public partial class FastResponse : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // Fast response - no delay
            lblMessage.Text = $"Fast response completed at {DateTime.Now:HH:mm:ss.fff}";
        }
    }
}

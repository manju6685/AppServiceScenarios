using System;
using System.Threading;

namespace AppServiceScenarios
{
    public partial class Slow10 : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Thread.Sleep(10000); // Sleep for 10 seconds
            lblMessage.Text = "Slept for 10 seconds";
        }
    }
}
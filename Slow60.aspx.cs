using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace AppServiceScenarios
{
    public partial class Slow60 : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Thread.Sleep(60000); // Sleep for 60 seconds
            lblMessage.Text = "Slept for 60 seconds";
        }
    }
}
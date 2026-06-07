using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace AppServiceScenarios
{
    public partial class Http500 : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // Simulate 100 internal errors for testing purposes
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    throw new Exception($"This is test HTTP 500 error #{i + 1} triggered by the user.");
                }
                catch (Exception ex)
                {
                    // Log the exception internally
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }

            // Set HTTP 500 status code without throwing exception (to show custom error page)
            Response.StatusCode = 500;
            Response.StatusDescription = "Internal Server Error";
        }
    }
}
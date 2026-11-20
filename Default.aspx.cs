using System;
using System.Web.UI;

namespace PrintIndustry
{
    public partial class Default : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Redirect(ResolveUrl("~/Views/Dashboard.aspx"), false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}

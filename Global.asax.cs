using System;
using System.Web;
using System.Web.Http;
using PrintIndustry.DAL;

namespace PrintIndustry
{
    public class Global : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            GlobalConfiguration.Configure(RegisterWebApi);

            Db.ConfigureDataDirectory(Server.MapPath("~/App_Data"));
            Db.Init();
        }

        private static void RegisterWebApi(HttpConfiguration config)
        {
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}

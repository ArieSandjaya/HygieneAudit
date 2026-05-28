using System;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using HygieneAudit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace WebApps
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(config =>
            {
                WebApiConfig.Register(config);
                AutofacConfig.Register(config);
            });
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            MigrateDatabase();
        }

        private static void MigrateDatabase()
        {
            try
            {
                var connStr = System.Configuration.ConfigurationManager
                    .ConnectionStrings["HygieneAuditConnection"]?.ConnectionString;
                if (string.IsNullOrEmpty(connStr)) return;

                var options = new DbContextOptionsBuilder<HygieneAuditDbContext>()
                    .UseSqlServer(connStr)
                    .Options;
                using (var db = new HygieneAuditDbContext(options))
                {
                    db.Database.Migrate();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[Warning] Database migration failed: {ex.Message}");
            }
        }
    }
}

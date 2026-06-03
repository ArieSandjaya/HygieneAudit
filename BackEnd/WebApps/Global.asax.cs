using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Web;
using System.Web.Helpers;
using System.Web.Hosting;
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
            AntiForgeryConfig.UniqueClaimTypeIdentifier = ClaimTypes.NameIdentifier;
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
            EnsureUploadsFolder();
            MigratePhotosToDisk();
        }

        private static void EnsureUploadsFolder()
        {
            var path = HostingEnvironment.MapPath("~/Uploads");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static void MigratePhotosToDisk()
        {
            var connStr = System.Configuration.ConfigurationManager
                .ConnectionStrings["HygieneAuditConnection"]?.ConnectionString;
            if (string.IsNullOrEmpty(connStr)) return;

            var uploadsPath = HostingEnvironment.MapPath("~/Uploads");

            var options = new DbContextOptionsBuilder<HygieneAuditDbContext>()
                .UseSqlServer(connStr).Options;

            using (var db = new HygieneAuditDbContext(options))
            {
                var legacy = db.Set<HygieneAudit.Domain.Entities.AuditItemPhoto>()
                    .Where(p => p.PhotoUrl.StartsWith("data:"))
                    .ToList();

                if (!legacy.Any()) return;

                foreach (var photo in legacy)
                {
                    try
                    {
                        photo.PhotoUrl = WebApps.Helpers.PhotoStorage.SaveFromDataUrl(
                            photo.PhotoUrl, uploadsPath);
                    }
                    catch { /* skip corrupt entries */ }
                }
                db.SaveChanges();
            }
        }

        private static void MigrateDatabase()
        {
            var connStr = System.Configuration.ConfigurationManager
                .ConnectionStrings["HygieneAuditConnection"]?.ConnectionString;
            if (string.IsNullOrEmpty(connStr)) return;

            var options = new DbContextOptionsBuilder<HygieneAuditDbContext>()
                .UseSqlServer(connStr)
                .Options;
            using (var db = new HygieneAuditDbContext(options))
            {
                // Throws if migration fails — surfaced as 500 on first request,
                // which is better than silently connecting to the wrong database.
                db.Database.Migrate();
            }
        }
    }
}

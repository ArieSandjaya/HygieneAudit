using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
            DisableFileMonitoringWhileDebugging();
            MigrateDatabase();
            EnsureUploadsFolder();
            MigratePhotosToDisk();
        }

        /// <summary>
        /// While a debugger is attached (F5), turn off ASP.NET's directory
        /// FileChangesMonitor. Writing an uploaded photo would otherwise be
        /// seen as an app change and recycle the AppDomain, which tears down
        /// IIS Express mid-request and "stops" the debugger. Only runs under
        /// the debugger, so production keeps its normal auto-restart behaviour.
        /// </summary>
        private static void DisableFileMonitoringWhileDebugging()
        {
            if (!System.Diagnostics.Debugger.IsAttached) return;
            try
            {
                var fcmProp = typeof(HttpRuntime).GetProperty("FileChangesMonitor",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var fcm = fcmProp?.GetValue(null, null);
                if (fcm == null) return;

                var subDirsField = fcm.GetType().GetField("_dirMonSubdirs",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                var monitor = subDirsField?.GetValue(fcm);
                if (monitor == null) return;

                var stop = monitor.GetType().GetMethod("StopMonitoring",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                stop?.Invoke(monitor, Array.Empty<object>());
            }
            catch
            {
                // Best-effort dev convenience — never let it break startup.
            }
        }

        protected void Application_EndRequest()
        {
            // Dispose the per-request Autofac lifetime scope created by the MVC resolver,
            // ensuring DbContext and other IDisposable services are released after each request.
            AutofacMvcDependencyResolver.DisposeRequestScope();
        }

        private static void EnsureUploadsFolder()
        {
            var path = WebApps.Helpers.PhotoStorage.UploadsFolder;
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static void MigratePhotosToDisk()
        {
            var connStr = System.Configuration.ConfigurationManager
                .ConnectionStrings["HygieneAuditConnection"]?.ConnectionString;
            if (string.IsNullOrEmpty(connStr)) return;

            var uploadsPath = WebApps.Helpers.PhotoStorage.UploadsFolder;

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

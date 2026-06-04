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
            DisableFileChangeMonitoring();
            MigrateDatabase();
            EnsureUploadsFolder();
            MigratePhotosToDisk();
            SeedDevData();
        }

        /// <summary>
        /// Turn off ASP.NET's recursive subdirectory FileChangesMonitor.
        /// Saving an uploaded photo — or any file written under the app tree —
        /// would otherwise be seen as an application change and recycle the
        /// AppDomain, which under IIS Express tears the worker process down
        /// mid-request (the "IIS Express dies on photo upload" symptom).
        ///
        /// Runs UNCONDITIONALLY (not just under the debugger): the previous
        /// debugger-only guard meant that running without F5 — or any case where
        /// the monitor was still active — recycled on upload again. Uploads are
        /// stored outside the web root anyway, so the only thing we trade away is
        /// auto-restart-on-file-change; recycle the app pool manually after
        /// changing Web.config or deploying new binaries.
        /// </summary>
        private static void DisableFileChangeMonitoring()
        {
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
                // Best-effort — never let it break startup.
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

        private static void SeedDevData()
        {
            var connStr = System.Configuration.ConfigurationManager
                .ConnectionStrings["HygieneAuditConnection"]?.ConnectionString;
            if (string.IsNullOrEmpty(connStr)) return;
            try
            {
                WebApps.Helpers.DevDataSeeder.Seed(connStr, WebApps.Helpers.PhotoStorage.UploadsFolder);
            }
            catch
            {
                // Seeding is best-effort — never crash startup.
            }
        }
    }
}

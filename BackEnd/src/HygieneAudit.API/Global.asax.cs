using System;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Http.Cors;
using Autofac;
using Autofac.Integration.WebApi;
using HygieneAudit.API.Filters;
using HygieneAudit.Application.Services;
using HygieneAudit.Domain.Interfaces;
using HygieneAudit.Infrastructure.Data;
using HygieneAudit.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace HygieneAudit.API
{
    public class Global : HttpApplication
    {
        internal static IConfiguration Configuration { get; private set; } = null!;

        protected void Application_Start(object sender, EventArgs e)
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Production.json", optional: true)
                .Build();

            var config = GlobalConfiguration.Configuration;

            ConfigureJson(config);
            config.MapHttpAttributeRoutes();
            ConfigureCors(config);
            ConfigureFilters(config);
            ConfigureDependencies(config);

            GlobalConfiguration.Configure(_ => { });

            MigrateDatabase();
        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            var response = HttpContext.Current.Response;
            response.Headers["X-Content-Type-Options"]   = "nosniff";
            response.Headers["X-Frame-Options"]          = "DENY";
            response.Headers["X-XSS-Protection"]         = "1; mode=block";
            response.Headers["Referrer-Policy"]          = "strict-origin-when-cross-origin";
            response.Headers["Permissions-Policy"]       = "camera=(), microphone=(), geolocation=()";
            response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        private static void ConfigureJson(HttpConfiguration config)
        {
            var settings = config.Formatters.JsonFormatter.SerializerSettings;
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            settings.NullValueHandling = NullValueHandling.Ignore;
            config.Formatters.Remove(config.Formatters.XmlFormatter);
        }

        private static void ConfigureCors(HttpConfiguration config)
        {
            var origins = Configuration.GetSection("Cors:AllowedOrigins")
                .GetChildren().Select(c => c.Value ?? "").Where(v => v.Length > 0).ToArray();
            if (origins.Length == 0) origins = new[] { "*" };

            bool allowAll = Array.IndexOf(origins, "*") >= 0;
            var corsAttr = allowAll
                ? new EnableCorsAttribute("*", "*", "*")
                : new EnableCorsAttribute(string.Join(",", origins), "*", "*") { SupportsCredentials = true };

            config.EnableCors(corsAttr);
        }

        private static void ConfigureFilters(HttpConfiguration config)
        {
            config.MessageHandlers.Add(new JwtAuthenticationHandler(Configuration));
            config.Filters.Add(new GlobalExceptionFilter());
        }

        private static void ConfigureDependencies(HttpConfiguration config)
        {
            var builder = new ContainerBuilder();

            builder.RegisterInstance(Configuration).As<IConfiguration>().SingleInstance();

            var connectionString = Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing from appsettings.json.");
            var dbOptions = new DbContextOptionsBuilder<HygieneAuditDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            builder.RegisterInstance(dbOptions).As<DbContextOptions<HygieneAuditDbContext>>().SingleInstance();
            builder.RegisterType<HygieneAuditDbContext>().InstancePerRequest();
            builder.RegisterType<UnitOfWork>().As<IUnitOfWork>().InstancePerRequest();
            builder.RegisterType<AuditService>().As<IAuditService>().InstancePerRequest();
            builder.RegisterType<AuthService>().As<IAuthService>().InstancePerRequest();
            builder.RegisterApiControllers(typeof(Global).Assembly);
            builder.RegisterWebApiFilterProvider(config);

            config.DependencyResolver = new AutofacWebApiDependencyResolver(builder.Build());
        }

        private static void MigrateDatabase()
        {
            try
            {
                var connectionString = Configuration.GetConnectionString("DefaultConnection");
                var options = new DbContextOptionsBuilder<HygieneAuditDbContext>()
                    .UseSqlServer(connectionString)
                    .Options;
                using var db = new HygieneAuditDbContext(options);
                db.Database.Migrate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[Warning] Database migration failed: {ex.Message}");
            }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Web.Http;
using Autofac;
using Autofac.Integration.WebApi;
using HygieneAudit.API.Filters;
using HygieneAudit.API.Middleware;
using HygieneAudit.Application.Services;
using HygieneAudit.Domain.Interfaces;
using HygieneAudit.Infrastructure.Data;
using HygieneAudit.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Owin;
using Microsoft.Owin.Cors;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.StaticFiles;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Owin;

[assembly: OwinStartup(typeof(HygieneAudit.API.Startup))]

namespace HygieneAudit.API
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var configuration = BuildConfiguration();
            var httpConfig = new HttpConfiguration();

            ConfigureJson(httpConfig);
            ConfigureRoutes(httpConfig);
            ConfigureFilters(httpConfig, configuration);
            ConfigureDependencies(httpConfig, configuration);
            ConfigureCors(app, configuration);
            app.Use<SecurityHeadersMiddleware>();
            ConfigureStaticFiles(app);

            app.UseWebApi(httpConfig);

            MigrateDatabase(configuration);
        }

        private static IConfiguration BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Production.json", optional: true)
                .Build();
        }

        private static void ConfigureJson(HttpConfiguration config)
        {
            var settings = config.Formatters.JsonFormatter.SerializerSettings;
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            settings.NullValueHandling = NullValueHandling.Ignore;
            config.Formatters.Remove(config.Formatters.XmlFormatter);
        }

        private static void ConfigureRoutes(HttpConfiguration config)
        {
            config.MapHttpAttributeRoutes();
        }

        private static void ConfigureDependencies(HttpConfiguration config, IConfiguration configuration)
        {
            var builder = new ContainerBuilder();

            builder.RegisterInstance(configuration).As<IConfiguration>().SingleInstance();

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var dbOptions = new DbContextOptionsBuilder<HygieneAuditDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            builder.RegisterInstance(dbOptions).As<DbContextOptions<HygieneAuditDbContext>>().SingleInstance();
            builder.RegisterType<HygieneAuditDbContext>().InstancePerRequest();
            builder.RegisterType<UnitOfWork>().As<IUnitOfWork>().InstancePerRequest();
            builder.RegisterType<AuditService>().As<IAuditService>().InstancePerRequest();
            builder.RegisterType<AuthService>().As<IAuthService>().InstancePerRequest();
            builder.RegisterApiControllers(typeof(Startup).Assembly);
            builder.RegisterWebApiFilterProvider(config);

            var container = builder.Build();
            config.DependencyResolver = new AutofacWebApiDependencyResolver(container);
        }

        private static void ConfigureFilters(HttpConfiguration config, IConfiguration configuration)
        {
            config.MessageHandlers.Add(new JwtAuthenticationHandler(configuration));
            config.Filters.Add(new GlobalExceptionFilter());
        }

        private static void ConfigureCors(IAppBuilder app, IConfiguration configuration)
        {
            var originsSection = configuration.GetSection("Cors:AllowedOrigins");
            var allowedOrigins = originsSection.GetChildren().Select(c => c.Value ?? "").Where(v => v.Length > 0).ToArray();
            if (allowedOrigins.Length == 0) allowedOrigins = new[] { "*" };
            bool allowAll = System.Array.IndexOf(allowedOrigins, "*") >= 0;

            app.UseCors(new CorsOptions
            {
                PolicyProvider = new CorsPolicyProvider
                {
                    PolicyResolver = _ =>
                    {
                        var policy = new System.Web.Cors.CorsPolicy
                        {
                            AllowAnyMethod = true,
                            AllowAnyHeader = true
                        };
                        if (allowAll)
                        {
                            policy.AllowAnyOrigin = true;
                        }
                        else
                        {
                            foreach (var origin in allowedOrigins)
                                policy.Origins.Add(origin);
                            policy.SupportsCredentials = true;
                        }
                        return System.Threading.Tasks.Task.FromResult(policy);
                    }
                }
            });
        }

        private static void ConfigureStaticFiles(IAppBuilder app)
        {
            var wwwroot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
            if (!Directory.Exists(wwwroot)) return;

            var fileSystem = new PhysicalFileSystem(wwwroot);
            app.UseDefaultFiles(new DefaultFilesOptions { FileSystem = fileSystem });
            app.UseStaticFiles(new StaticFileOptions { FileSystem = fileSystem });
        }

        private static void MigrateDatabase(IConfiguration configuration)
        {
            try
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                var options = new DbContextOptionsBuilder<HygieneAuditDbContext>()
                    .UseSqlServer(connectionString)
                    .Options;
                using (var db = new HygieneAuditDbContext(options))
                {
                    db.Database.Migrate();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Database migration failed: {ex.Message}");
            }
        }
    }
}

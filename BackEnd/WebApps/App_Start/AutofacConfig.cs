using Autofac;
using Autofac.Integration.WebApi;
using HygieneAudit.Application.Services;
using HygieneAudit.Domain.Interfaces;
using HygieneAudit.Infrastructure.Data;
using HygieneAudit.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Mvc;

namespace WebApps
{
    public static class AutofacConfig
    {
        public static IContainer Container { get; private set; }

        public static void Register(HttpConfiguration httpConfig)
        {
            var builder = new ContainerBuilder();

            // Bridge Web.config appSettings to IConfiguration (AuthService needs IConfiguration)
            var inMemory = new Dictionary<string, string>
            {
                ["Jwt:Key"]           = System.Configuration.ConfigurationManager.AppSettings["Jwt:Key"],
                ["Jwt:Issuer"]        = System.Configuration.ConfigurationManager.AppSettings["Jwt:Issuer"],
                ["Jwt:Audience"]      = System.Configuration.ConfigurationManager.AppSettings["Jwt:Audience"],
                ["Jwt:ExpiryMinutes"] = System.Configuration.ConfigurationManager.AppSettings["Jwt:ExpiryMinutes"],
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemory)
                .Build();
            builder.RegisterInstance(configuration).As<IConfiguration>().SingleInstance();

            // EF Core DbContext options (singleton)
            var connStr = System.Configuration.ConfigurationManager
                .ConnectionStrings["HygieneAuditConnection"].ConnectionString;
            var dbOptions = new DbContextOptionsBuilder<HygieneAuditDbContext>()
                .UseSqlServer(connStr)
                .Options;
            builder.RegisterInstance(dbOptions).As<DbContextOptions<HygieneAuditDbContext>>().SingleInstance();

            // Domain services - InstancePerDependency so each controller gets a fresh set
            builder.RegisterType<HygieneAuditDbContext>().InstancePerDependency();
            builder.RegisterType<UnitOfWork>().As<IUnitOfWork>().InstancePerDependency();
            builder.RegisterType<AuditService>().As<IAuditService>().InstancePerDependency();
            builder.RegisterType<AuthService>().As<IAuthService>().InstancePerDependency();

            // Register MVC controllers (all Controller subclasses in this assembly)
            var assembly = typeof(MvcApplication).Assembly;
            builder.RegisterAssemblyTypes(assembly)
                .Where(t => typeof(Controller).IsAssignableFrom(t))
                .AsSelf()
                .InstancePerDependency();

            // Register Web API controllers
            builder.RegisterApiControllers(assembly);
            builder.RegisterWebApiFilterProvider(httpConfig);

            Container = builder.Build();

            // MVC dependency resolver (custom - no Autofac.Mvc5 needed)
            DependencyResolver.SetResolver(new AutofacMvcDependencyResolver(Container));

            // Web API dependency resolver
            httpConfig.DependencyResolver = new AutofacWebApiDependencyResolver(Container);
        }
    }

    // Custom MVC IDependencyResolver backed by Autofac (replaces Autofac.Mvc5)
    public sealed class AutofacMvcDependencyResolver : System.Web.Mvc.IDependencyResolver
    {
        private readonly IContainer _container;

        public AutofacMvcDependencyResolver(IContainer container) => _container = container;

        public object GetService(Type serviceType)
        {
            try { return _container.ResolveOptional(serviceType); }
            catch { return null; }
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            try
            {
                var collectionType = typeof(IEnumerable<>).MakeGenericType(serviceType);
                var result = _container.ResolveOptional(collectionType);
                return result != null ? (IEnumerable<object>)result : Enumerable.Empty<object>();
            }
            catch { return Enumerable.Empty<object>(); }
        }
    }
}

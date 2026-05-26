using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Owin.Hosting;

namespace HygieneAudit.API
{
    class Program
    {
        static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Production.json", optional: false)
                .Build();

            var port = configuration["Server:Port"] ?? "5000";
            var url = $"http://localhost:{port}";

            using (WebApp.Start<Startup>(url))
            {
                Console.WriteLine($"HygieneAudit API running on {url}");
                Console.WriteLine("Press Enter to stop...");
                Console.ReadLine();
            }
        }
    }
}

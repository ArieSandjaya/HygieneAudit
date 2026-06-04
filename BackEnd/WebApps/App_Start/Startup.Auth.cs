using Microsoft.AspNet.Identity;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using Owin;
using System;

namespace WebApps
{
    public partial class Startup
    {
        public void ConfigureAuth(IAppBuilder app)
        {
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie,
                LoginPath = new PathString("/Account/Login"),
                ExpireTimeSpan = TimeSpan.FromMinutes(480),
                SlidingExpiration = true,
                // Send cookie only over HTTPS in production; also over HTTP in development.
                CookieSecure = CookieSecureOption.SameAsRequest,
                // Restrict cookie to same-site requests to mitigate CSRF.
                CookieSameSite = SameSiteMode.Lax,
                Provider = new CookieAuthenticationProvider()
            });
        }
    }
}

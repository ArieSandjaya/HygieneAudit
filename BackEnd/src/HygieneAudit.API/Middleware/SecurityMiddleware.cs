using System.Threading.Tasks;
using Microsoft.Owin;

namespace HygieneAudit.API.Middleware
{
    // Security headers injected at the OWIN pipeline level.
    public class SecurityHeadersMiddleware : OwinMiddleware
    {
        public SecurityHeadersMiddleware(OwinMiddleware next) : base(next) { }

        public override async Task Invoke(IOwinContext context)
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

            await Next.Invoke(context);
        }
    }
}

using System.Linq;
using System.Security.Claims;
using System.Web.Mvc;

namespace WebApps.Filters
{
    public class DomainAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(System.Web.HttpContextBase httpContext)
        {
            if (!httpContext.User.Identity.IsAuthenticated) return false;
            if (string.IsNullOrEmpty(Roles)) return true;

            var identity = httpContext.User.Identity as ClaimsIdentity;
            var role = identity?.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(role)) return false;

            return Roles.Split(',').Any(r => r.Trim() == role);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (!filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                base.HandleUnauthorizedRequest(filterContext);
            }
            else
            {
                filterContext.Result = new HttpStatusCodeResult(403, "Forbidden");
            }
        }
    }
}

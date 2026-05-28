using HygieneAudit.Application.Exceptions;
using System.Net;
using System.Net.Http;
using System.Web.Http.Filters;

namespace WebApps.Filters
{
    public class DomainExceptionFilter : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            if (context.Exception is ValidationException)
            {
                context.Response = context.Request.CreateResponse(
                    HttpStatusCode.BadRequest,
                    new { message = context.Exception.Message });
            }
            else if (context.Exception is NotFoundException)
            {
                context.Response = context.Request.CreateResponse(
                    HttpStatusCode.NotFound,
                    new { message = context.Exception.Message });
            }
            else
            {
                context.Response = context.Request.CreateResponse(
                    HttpStatusCode.InternalServerError,
                    new { message = "Terjadi kesalahan pada server." });
            }
        }
    }
}

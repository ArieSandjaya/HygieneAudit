using System.Net;
using System.Net.Http;
using System.Web.Http.Filters;
using HygieneAudit.Application.Exceptions;

namespace HygieneAudit.API.Filters
{
    public class GlobalExceptionFilter : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            if (context.Exception is ValidationException validationEx)
            {
                context.Response = context.Request.CreateResponse(
                    HttpStatusCode.BadRequest,
                    new { message = validationEx.Message });
            }
            else if (context.Exception is NotFoundException notFoundEx)
            {
                context.Response = context.Request.CreateResponse(
                    HttpStatusCode.NotFound,
                    new { message = notFoundEx.Message });
            }
            else
            {
                context.Response = context.Request.CreateResponse(
                    HttpStatusCode.InternalServerError,
                    new { message = "Internal server error." });
            }
        }
    }
}

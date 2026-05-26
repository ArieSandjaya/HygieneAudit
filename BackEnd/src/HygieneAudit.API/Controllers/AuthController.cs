using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using HygieneAudit.Application.Services;
using HygieneAudit.Domain.DTOs;

namespace HygieneAudit.API.Controllers
{
    [RoutePrefix("api/auth")]
    public class AuthController : ApiController
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost]
        [Route("login")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> Login([FromBody] LoginRequest request)
        {
            var response = await _authService.AuthenticateAsync(request.Username, request.Password);
            if (response == null)
                return Content(HttpStatusCode.Unauthorized, new { message = "Username atau password salah!" });

            return Ok(response);
        }
    }
}

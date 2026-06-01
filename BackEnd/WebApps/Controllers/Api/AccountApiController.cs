using HygieneAudit.Application.DTOs;
using HygieneAudit.Domain.Interfaces;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;

namespace WebApps.Controllers.Api
{
    [RoutePrefix("api/account")]
    [Authorize]
    public class AccountApiController : ApiController
    {
        private readonly IUnitOfWork _uow;

        public AccountApiController(IUnitOfWork uow) => _uow = uow;

        [HttpPost, Route("change-password")]
        public async Task<IHttpActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.OldPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
                return Content(HttpStatusCode.BadRequest, new { message = "Password lama dan baru wajib diisi." });

            if (req.NewPassword.Length < 6)
                return Content(HttpStatusCode.BadRequest, new { message = "Password baru minimal 6 karakter." });

            var identity = System.Web.HttpContext.Current?.User?.Identity as ClaimsIdentity
                           ?? User.Identity as ClaimsIdentity;
            if (!int.TryParse(identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                return Unauthorized();

            var user = await _uow.Users.GetByIdAsync(userId);
            if (user == null) return Unauthorized();

            if (!BCrypt.Net.BCrypt.Verify(req.OldPassword, user.PasswordHash))
                return Content(HttpStatusCode.BadRequest, new { message = "Password lama tidak benar." });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            await _uow.Users.UpdateAsync(user);
            await _uow.SaveChangesAsync();

            return Ok(new { message = "Password berhasil diubah." });
        }
    }
}

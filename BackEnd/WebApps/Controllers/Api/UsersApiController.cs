using HygieneAudit.Application.DTOs;
using HygieneAudit.Domain.Entities;
using HygieneAudit.Domain.Interfaces;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;

namespace WebApps.Controllers.Api
{
    [RoutePrefix("api/users")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class UsersApiController : ApiController
    {
        private readonly IUnitOfWork _uow;

        public UsersApiController(IUnitOfWork uow) => _uow = uow;

        [HttpGet, Route("")]
        public async Task<IHttpActionResult> GetAll()
        {
            var users = await _uow.Users.GetAllAsync();
            var result = users.Select(u => new UserResponse
            {
                Id = u.Id,
                Username = u.Username,
                Name = u.Name,
                Role = u.Role.ToString(),
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
            });
            return Ok(result);
        }

        [HttpPost, Route("")]
        public async Task<IHttpActionResult> Create([FromBody] CreateUserRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return Content(HttpStatusCode.BadRequest, new { message = "Username dan password wajib diisi." });

            var all = await _uow.Users.GetAllAsync();
            if (all.Any(u => u.Username == req.Username))
                return Content(HttpStatusCode.Conflict, new { message = "Username sudah dipakai." });

            if (!System.Enum.TryParse<UserRole>(req.Role, true, out var role))
                role = UserRole.Auditor;

            var user = new User
            {
                Username = req.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                Name = req.Name,
                Role = role,
            };

            await _uow.Users.AddAsync(user);
            await _uow.SaveChangesAsync();

            return Created(
                new System.Uri($"api/users/{user.Id}", System.UriKind.Relative),
                new UserResponse { Id = user.Id, Username = user.Username, Name = user.Name, Role = user.Role.ToString(), IsActive = user.IsActive, CreatedAt = user.CreatedAt });
        }

        [HttpPut, Route("{id:int}")]
        public async Task<IHttpActionResult> Update(int id, [FromBody] UpdateUserRequest req)
        {
            var user = await _uow.Users.GetByIdAsync(id);
            if (user == null) return NotFound();

            if (req.Name != null) user.Name = req.Name;
            if (req.IsActive != null) user.IsActive = req.IsActive.Value;
            if (req.Role != null && System.Enum.TryParse<UserRole>(req.Role, true, out var role))
                user.Role = role;
            if (!string.IsNullOrWhiteSpace(req.Password))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

            await _uow.Users.UpdateAsync(user);
            await _uow.SaveChangesAsync();

            return Ok(new UserResponse { Id = user.Id, Username = user.Username, Name = user.Name, Role = user.Role.ToString(), IsActive = user.IsActive, CreatedAt = user.CreatedAt });
        }

        [HttpDelete, Route("{id:int}")]
        public async Task<IHttpActionResult> Delete(int id)
        {
            var user = await _uow.Users.GetByIdAsync(id);
            if (user == null) return NotFound();

            user.IsActive = false;
            await _uow.Users.UpdateAsync(user);
            await _uow.SaveChangesAsync();

            return StatusCode(HttpStatusCode.NoContent);
        }
    }
}

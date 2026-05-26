using HygieneAudit.Application.DTOs;
using HygieneAudit.Domain.Entities;
using HygieneAudit.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HygieneAudit.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class UsersController : ControllerBase
{
    private readonly IUnitOfWork _uow;

    public UsersController(IUnitOfWork uow) => _uow = uow;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _uow.Users.GetAllAsync();
        var result = users.Select(u => new UserResponse
        {
            Id        = u.Id,
            Username  = u.Username,
            Name      = u.Name,
            Role      = u.Role.ToString(),
            IsActive  = u.IsActive,
            CreatedAt = u.CreatedAt,
        });
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Username dan password wajib diisi." });

        var all = await _uow.Users.GetAllAsync();
        if (all.Any(u => u.Username == req.Username))
            return Conflict(new { message = "Username sudah dipakai." });

        if (!Enum.TryParse<UserRole>(req.Role, true, out var role))
            role = UserRole.Auditor;

        var user = new User
        {
            Username     = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Name         = req.Name,
            Role         = role,
        };

        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();

        return Created(
            $"api/users/{user.Id}",
            new UserResponse
            {
                Id = user.Id, Username = user.Username, Name = user.Name,
                Role = user.Role.ToString(), IsActive = user.IsActive, CreatedAt = user.CreatedAt
            });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest req)
    {
        var user = await _uow.Users.GetByIdAsync(id);
        if (user == null) return NotFound();

        if (req.Name     != null) user.Name     = req.Name;
        if (req.IsActive != null) user.IsActive  = req.IsActive.Value;
        if (req.Role     != null && Enum.TryParse<UserRole>(req.Role, true, out var role))
            user.Role = role;
        if (!string.IsNullOrWhiteSpace(req.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

        await _uow.Users.UpdateAsync(user);
        await _uow.SaveChangesAsync();

        return Ok(new UserResponse
        {
            Id = user.Id, Username = user.Username, Name = user.Name,
            Role = user.Role.ToString(), IsActive = user.IsActive, CreatedAt = user.CreatedAt
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _uow.Users.GetByIdAsync(id);
        if (user == null) return NotFound();

        user.IsActive = false;
        await _uow.Users.UpdateAsync(user);
        await _uow.SaveChangesAsync();

        return NoContent();
    }
}

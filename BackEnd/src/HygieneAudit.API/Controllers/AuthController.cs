using HygieneAudit.Application.Services;
using HygieneAudit.Domain.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HygieneAudit.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await _authService.AuthenticateAsync(request.Username, request.Password);
        if (response == null)
            return Unauthorized(new { message = "Username atau password salah!" });

        return Ok(response);
    }
}

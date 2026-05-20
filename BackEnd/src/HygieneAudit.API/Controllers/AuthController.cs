using HygieneAudit.Application.Services;
using HygieneAudit.Domain.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace HygieneAudit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var response = await _authService.AuthenticateAsync(request.Username, request.Password);
        if (response == null)
            return Unauthorized(new { message = "Username atau password salah!" });

        return Ok(response);
    }
}

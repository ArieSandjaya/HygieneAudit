using HygieneAudit.Domain.DTOs;
using HygieneAudit.Domain.Entities;

namespace HygieneAudit.Application.Services;

public interface IAuthService
{
    Task<AuthResponse?> AuthenticateAsync(string username, string password);
    string GenerateJwtToken(User user);
}

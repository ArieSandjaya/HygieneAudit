using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HygieneAudit.API.Filters
{
    public class JwtAuthenticationHandler : DelegatingHandler
    {
        private readonly IConfiguration _config;

        public JwtAuthenticationHandler(IConfiguration config)
        {
            _config = config;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var authHeader = request.Headers.Authorization;
            if (authHeader?.Scheme == "Bearer" && !string.IsNullOrEmpty(authHeader.Parameter))
            {
                try
                {
                    var principal = ValidateToken(authHeader.Parameter);
                    Thread.CurrentPrincipal = principal;
                    if (HttpContext.Current != null)
                        HttpContext.Current.User = principal;
                }
                catch
                {
                    // Invalid token — [Authorize] will reject the request with 401
                }
            }

            return await base.SendAsync(request, cancellationToken);
        }

        private IPrincipal ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]);

            SecurityToken validatedToken;
            return tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _config["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _config["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = ClaimTypes.Role,
                NameClaimType = ClaimTypes.Name
            }, out validatedToken);
        }
    }
}

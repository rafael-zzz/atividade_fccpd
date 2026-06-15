namespace Shared.Auth;

using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public static class JwtHelper
{
    // Lê da variável de ambiente; fallback para dev
    private static string Secret =>
        Environment.GetEnvironmentVariable("JWT_SECRET")
        ?? "MiniEcommerceSecretKey2024SuperSegura!@#$%";

    /// <summary>
    /// Gera um token JWT com claims de userId, email e role. Expira em 1h.
    /// </summary>
    public static string GenerateToken(string userId, string email, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("userId", userId),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role),
        };

        var token = new JwtSecurityToken(
            issuer: "mini-ecommerce",
            audience: "mini-ecommerce",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Retorna parâmetros de validação reutilizáveis por todos os serviços.
    /// </summary>
    public static TokenValidationParameters GetValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "mini-ecommerce",
            ValidateAudience = true,
            ValidAudience = "mini-ecommerce",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
        };
    }
}

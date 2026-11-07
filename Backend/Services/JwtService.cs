using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CoCaNgua.Server.Models;
using Microsoft.IdentityModel.Tokens;

namespace CoCaNgua.Server.Services;

public interface IJwtService
{
    string GenerateToken(User user);
}

public class JwtService : IJwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(User user)
    {
        string secret = _config["Jwt:Secret"] ?? "GNS301_Lab_SU26_Default_Secret_Key_At_Least_32_Chars!";
        string issuer = _config["Jwt:Issuer"] ?? "GNS301_Lab";
        int expireMinutes = int.TryParse(_config["Jwt:ExpireMinutes"], out int exp) ? exp : 60;

        // Ensure key is at least 32 bytes for HMAC-SHA256
        if (secret.Length < 32)
        {
            secret = secret.PadRight(32, '!');
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("fullName", user.FullName ?? ""),
            new Claim("role", user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: null,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expireMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

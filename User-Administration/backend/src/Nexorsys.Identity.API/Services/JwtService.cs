using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Nexorsys.Identity.Core;

namespace Nexorsys.Identity.API.Services;

public interface IJwtService
{
	string GenerateToken(User user);
	ClaimsPrincipal ValidateToken(string token);
}

public class JwtService : IJwtService
{
	private readonly IConfiguration _configuration;

	public JwtService(IConfiguration configuration)
	{
		_configuration = configuration;
	}

	public string GenerateToken(User user)
	{
		var tokenHandler = new JwtSecurityTokenHandler();
		var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);
		var tokenDescriptor = new SecurityTokenDescriptor
		{
			Subject = new ClaimsIdentity(new[]
			{
				new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
				new Claim(ClaimTypes.Name, user.SamAccountName),
				new Claim("user_id", user.Id.ToString())
			}),
			Expires = DateTime.UtcNow.AddHours(1),
			Issuer = _configuration["Jwt:Issuer"],
			Audience = _configuration["Jwt:Audience"],
			SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
		};
		var token = tokenHandler.CreateToken(tokenDescriptor);
		return tokenHandler.WriteToken(token);
	}

	public ClaimsPrincipal ValidateToken(string token)
	{
		var tokenHandler = new JwtSecurityTokenHandler();
		var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);
		var validationParameters = new TokenValidationParameters
		{
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(key),
			ValidateIssuer = true,
			ValidateAudience = true,
			ValidIssuer = _configuration["Jwt:Issuer"],
			ValidAudience = _configuration["Jwt:Audience"],
			ClockSkew = TimeSpan.Zero
		};
		return tokenHandler.ValidateToken(token, validationParameters, out _);
	}
}

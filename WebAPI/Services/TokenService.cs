using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WebAPI.Infrastructure;
using WebAPI.Models;

namespace WebAPI.Services;

public class TokenService(IUserRepository userRepository, IConfiguration configuration)
{
    public async Task<string> GenerateTokenAsync(string userId)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]));
        var user = await userRepository.GetUserAsync(userId);
        var expiry = int.Parse(configuration["Jwt:ExpiryInSeconds"]);
        var claims = new Claim[]
        {
            new(ClaimConstants.UserId, user.Id.ToString()),
            new(ClaimConstants.UserName, user.UserName)
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            claims: claims,
            audience: configuration["Jwt:Audience"],
            expires: DateTime.Now.Add(TimeSpan.FromSeconds(expiry)),
            signingCredentials: new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

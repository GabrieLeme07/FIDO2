using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Infrastructure;
using WebAPI.Models;

namespace WebAPI.Controllers;

[Authorize]
[Route("/api/users")]
public class UserController(IUserRepository userRepository) : ControllerBase
{
    [HttpGet("me")]
    public async Task<User> GetCurrentUser()
    {
        var userName = User.Claims.First(claim => claim.Type == ClaimConstants.UserName).Value;
        var user = await userRepository.GetUserAsync(userName);

        return user;
    }
}

using Fido2NetLib;
using WebAPI.Models;

namespace WebAPI.Extensions;

public static class UserExtensions
{
    public static Fido2User ToFido2User(this User user)
    {
        return new Fido2User
        {
            Id =new Guid(user.Id).ToByteArray(),
            Name = user.UserName,
            DisplayName = user.DisplayName
        };
    }
}

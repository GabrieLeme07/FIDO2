using Fido2NetLib;

namespace WebAPI.Models;

public record AssertionOptionsResponse(AssertionOptions AssertionOptions, string UserId);
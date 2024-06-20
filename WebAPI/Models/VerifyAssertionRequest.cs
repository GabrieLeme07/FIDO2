using Fido2NetLib;

namespace WebAPI.Models;

public record VerifyAssertionRequest(AuthenticatorAssertionRawResponse AssertionRawResponse, string UserId);
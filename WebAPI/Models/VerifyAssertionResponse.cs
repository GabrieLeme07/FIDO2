using Fido2NetLib.Objects;

namespace WebAPI.Models;

public record VerifyAssertionResponse(AssertionVerificationResult AssertionVerificationResult, string Token);
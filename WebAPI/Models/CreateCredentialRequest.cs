using Fido2NetLib;

namespace WebAPI.Models;

public record CreateCredentialRequest(AuthenticatorAttestationRawResponse AttestationResponse, string UserId);
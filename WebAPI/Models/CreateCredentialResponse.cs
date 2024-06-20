using Fido2NetLib;

namespace WebAPI.Models;

public record CreateCredentialResponse(Fido2.CredentialMakeResult CredentialMakeResult, string Token);
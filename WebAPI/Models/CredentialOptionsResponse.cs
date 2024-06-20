using Fido2NetLib;

namespace WebAPI.Models;

public record CredentialOptionsResponse(CredentialCreateOptions Options, string UserId);
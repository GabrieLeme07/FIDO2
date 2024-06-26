using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using WebAPI.Extensions;
using WebAPI.Infrastructure;
using WebAPI.Models;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("/api/fido2")]
public class Fido2Controller(
    IFido2 fido2,
    IUserRepository userRepository,
    IDistributedCache cache,
    TokenService tokenService,
    OtpService otpService,
    EmailService emailService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("request-otp")]
    public async Task<IActionResult> RequestOtp([FromBody] string userName)
    {
        // Gera um código OTP e envia por email
        // Neste serviço faremos a consulta do email do usuário com base no userName informado
        //HTMAC
        //Rate Limit
        //Verificar região de acesso

        //Envia o código OTP para telefone/email
        //Retorna hash para o browser
        var otp = await otpService.GenerateOtp(userName);
        await emailService.SendOtpEmail(otp);

        // Cria um JWT contendo o userName e permissão para validar o OTP
        var token = await tokenService.GenerateOTPTokenAsync(userName);
        return Ok(token);
    }

    [HttpPost("validate-otp")]
    [Authorize(Roles = "CanValidateOtp")]
    public async Task<IActionResult> ValidateOtp([FromBody] ValidateOtpRequest request)
    {
        var userName = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userName == null)
        {
            return Unauthorized();
        }

        // Recupere o hash armazenado associado ao userName
        var storedHash = RetrieveStoredHashForUser(userName);
        if (storedHash == null)
        {
            return BadRequest("OTP not requested or expired");
        }

        if (otpService.ValidateOtp(request.Otp, storedHash))
        {
            // Cria um JWT contendo o userName e permissão para validar seguir com o Passkey
            var token = await tokenService.GeneratePasskeyTokenAsync(userName);
            return Ok(token);
        }

        return BadRequest();
    }

    [HttpPost("register/begin")]
    [Authorize(Roles = "CanAccessPasskey")]
    public async Task<ActionResult<CredentialOptionsResponse>> CreateCredentialOptions()
    {
        var userName = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userName == null)
        {
            return Unauthorized();
        }

        // Valida se o userName não é vazio
        if (string.IsNullOrEmpty(userName))
        {
            return BadRequest("Username is required to create an authentication options");
        }

        // Valida se existe algum usuário com esse userName
        var existingUser = await userRepository.GetUserAsync(userName);
        if (existingUser == null)
        {
            return BadRequest($"Username {userName} not found!");
        }

        var fido2User = existingUser.ToFido2User();

        var excludedCredentials = existingUser
            .Credentials
            .Select(credential => new PublicKeyCredentialDescriptor(Convert.FromBase64String(credential.Id)))
            .ToList();

        var credentialOptions = fido2.RequestNewCredential(fido2User, excludedCredentials);

        await cache.SetAsync(existingUser.Uuid.ToString(), Encoding.UTF8.GetBytes(credentialOptions.ToJson()));

        return Ok(new CredentialOptionsResponse(credentialOptions, existingUser.Uuid));
    }

    [HttpPost("register/end")]
    [Authorize(Roles = "CanAccessPasskey")]
    public async Task<ActionResult<CreateCredentialResponse>> CreateCredential([FromBody] CreateCredentialRequest createCredentialRequest)
    {
        var userKey = createCredentialRequest.UserId.ToString();

        var credentialOptionsBytes = await cache.GetAsync(userKey);

        if (credentialOptionsBytes == null)
        {
            return BadRequest();
        }

        await cache.RemoveAsync(userKey);

        var attestationResponse = createCredentialRequest.AttestationResponse;

        var credentialOptions = CredentialCreateOptions.FromJson(Encoding.UTF8.GetString(credentialOptionsBytes));

        var credential = await fido2.MakeNewCredentialAsync(attestationResponse, credentialOptions, (args, _) => userRepository.IsCredentialIdUniqueToUserAsync(args.User.Id, Convert.ToBase64String(args.CredentialId)));

        if (credential.Result != null)
        {
            await userRepository.AddCredentialToUserAsync(
                createCredentialRequest.UserId,
                Convert.ToBase64String(credential.Result.CredentialId),
                credential.Result.PublicKey,
                credential.Result.Counter,
                (HttpContext.Items[Constants.Device.PlatformInfoKey] as string)!);

            var token = await tokenService.GenerateTokenAsync(createCredentialRequest.UserId);

            return Created("", new CreateCredentialResponse(credential, token));
        }

        return BadRequest();
    }

    [HttpPost("autenticate/begin")]
    [Authorize(Roles = "CanAccessPasskey")]
    public async Task<ActionResult<AssertionOptionsResponse>> CreateAssertionOptions([FromBody] string userName, [FromQuery] UserVerificationRequirement userVerificationRequirement = UserVerificationRequirement.Required)
    {
        if (string.IsNullOrEmpty(userName))
        {
            return BadRequest("Username is required to create an authentication options");
        }

        var user = await userRepository.GetUserAsync(userName);
        if (user == null)
        {
            return BadRequest("No such user found");
        }

        var existingCredentials = user.Credentials.Select(credential => new PublicKeyCredentialDescriptor(Convert.FromBase64String(credential.Id)));
        var options = fido2.GetAssertionOptions(existingCredentials, userVerificationRequirement);
        await cache.SetAsync(user.Uuid.ToString(), Encoding.UTF8.GetBytes(options.ToJson()));
        return Ok(new AssertionOptionsResponse(options, user.Uuid));
    }

    [HttpPost("autenticate/end")]
    [Authorize(Roles = "CanAccessPasskey")]
    public async Task<ActionResult<VerifyAssertionResponse>> VerifyAssertion([FromBody] VerifyAssertionRequest verificationRequest)
    {
        var userKey = verificationRequest.UserId.ToString();
        var assertionOptionBytes = await cache.GetAsync(userKey);
        await cache.RemoveAsync(userKey);
        if (assertionOptionBytes == null)
        {
            return BadRequest();
        }

        var assertionOptions = AssertionOptions.FromJson(Encoding.UTF8.GetString(assertionOptionBytes));
        var credential = await userRepository.GetCredentialAsync(Convert.ToBase64String(verificationRequest.AssertionRawResponse.Id));

        var assertionResult = await fido2.MakeAssertionAsync(verificationRequest.AssertionRawResponse, assertionOptions, credential.PublicKey, credential.SignCounter,
            (args, _) => Task.FromResult(new Guid(args.UserHandle).ToString() == credential.UserId));

        credential.SignCounter = assertionResult.Counter;

        credential.LastUsedPlatformInfo = HttpContext.Items[Constants.Device.PlatformInfoKey] as string;

        await userRepository.UpdateCredentialAsync(credential);

        var token = await tokenService.GenerateTokenAsync(verificationRequest.UserId);

        return Ok(new VerifyAssertionResponse(assertionResult, token));
    }

    [HttpPut("credential-options")]
    public async Task<ActionResult<CredentialOptionsResponse>> UpdateCredentialOptions()
    {
        var user = await userRepository.GetUserAsync(User.Claims.First(claim => claim.Type == ClaimConstants.UserName).Value);
        var excludedCredentials = user.Credentials;
        var credentialOptions = fido2.RequestNewCredential(user.ToFido2User(),
            excludedCredentials.Select(credential => new PublicKeyCredentialDescriptor(Convert.FromBase64String(credential.Id))).ToList());
        await cache.SetAsync(user.Id.ToString(), Encoding.UTF8.GetBytes(credentialOptions.ToJson()));
        return Ok(new CredentialOptionsResponse(credentialOptions, user.Id));
    }

    [HttpDelete("credential")]
    [Authorize(Roles = "CanAccessPasskey")]
    public async Task<IActionResult> RevokeCredential([FromBody] string encodedCredentialId)
    {
        var userId = Guid.Parse(User.Claims.First(claim => claim.Type == ClaimConstants.UserId).Value);
        var credentialRevokeResponse = await userRepository.RevokeCredentialAsync(userId.ToString(), encodedCredentialId);
        return credentialRevokeResponse switch
        {
            CredentialRevokeResult.Success => NoContent(),
            CredentialRevokeResult.NotFound => NotFound("No such credential found for user!"),
            CredentialRevokeResult.CannotRevokePrimary => BadRequest("Cannot Revoke the only remaining credential!"),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private string RetrieveStoredHashForUser(string userName)
    {
        // Implemente a lógica para recuperar o hash armazenado do banco de dados
        return "storedHash"; // Exemplo de hash armazenado
    }
}

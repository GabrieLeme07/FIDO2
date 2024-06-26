using System.Security.Cryptography;
using System.Text;

namespace WebAPI.Services;

public class OtpService 
{
    private readonly string _secretKey = "CHAVE_PARA_HMAC"; // Key for HMACSHA256

    public Task<string> GenerateOtp(string userName)
    {
        var otpCode = "12345";
        var otpHash = HashOtp(otpCode);

        //Salva OTP/Hash/UserName
        return Task.FromResult(otpCode);
    }

    public string HashOtp(string otp)
    {
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey)))
        {
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(otp));
            return Convert.ToBase64String(hash);
        }
    }

    public bool ValidateOtp(string otp, string hash)
    {
        var hashOfInput = HashOtp(otp);
        return hash == hashOfInput;
    }
}

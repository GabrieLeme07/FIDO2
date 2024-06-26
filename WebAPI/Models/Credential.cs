namespace WebAPI.Models;

public class Credential
{
    public string Id { get; set; }
    public byte[] PublicKey { get; set; }
    public string UserId { get; set; }
    public uint SignCounter { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string LastUsedPlatformInfo { get; set; }
}

using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using WebAPI.Models;

namespace WebAPI.Infrastructure;

public interface IUserRepository
{
    Task<User> CreateUserAsync(string userName);

    Task<User?> GetUserAsync(string userName);

    ValueTask<Credential?> GetCredentialAsync(byte[] credentialId);

    Task<bool> IsCredentialIdUniqueToUserAsync(byte[] userHandle, byte[] credentialId);

    Task<bool> VerifyCredentialOwnershipAsync(byte[] userHandle, byte[] credentialId);

    Task UpdateCredentialAsync(Credential credential);

    Task AddCredentialToUserAsync(string userId, byte[] credentialId, byte[] publicKey, uint signCounter, string lastUsedPlatformInfo);

    Task<CredentialRevokeResult> RevokeCredentialAsync(string userId, byte[] credentialId);
}

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<Credential> _credentials;

    public UserRepository(IPassKeysDbContext database)
    {
        _users = database.Collection<User>();
        _credentials = database.Collection<Credential>();
    }

    public async Task<User> CreateUserAsync(string userName)
    {
        var user = new User
        {
            Uuid = Guid.NewGuid().ToString(),
            UserName = userName,
            Credentials = new List<Credential>(),
            DisplayName = userName
        };

        await _users.InsertOneAsync(user);
        return user;
    }

    public async Task<User?> GetUserAsync(string userName)
    {
        var filterBuilder = Builders<User>.Filter;
        var filter = filterBuilder.Eq(u => u.UserName, userName);

        return await _users.Find(filter).FirstOrDefaultAsync();
    }

    public async ValueTask<Credential?> GetCredentialAsync(byte[] credentialId)
    {
        var filterBuilder = Builders<Credential>.Filter;
        var filter = filterBuilder.Eq(u => u.Id, credentialId);

        return await _credentials.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<bool> IsCredentialIdUniqueToUserAsync(byte[] userHandle, byte[] credentialId)
    {
        var userId = new Guid(userHandle);
        return await _credentials.AsQueryable().CountAsync(credential => credential.UserId == userId.ToString() && credential.Id == credentialId) == 0;
    }

    public async Task<bool> VerifyCredentialOwnershipAsync(byte[] userHandle, byte[] credentialId)
    {
        var userId = new Guid(userHandle);
        var credential = await GetCredentialAsync(credentialId);
        if (credential != null)
        {
            return credential.UserId == userId.ToString();
        }

        return false;
    }

    public async Task UpdateCredentialAsync(Credential credential)
    {
        var filter = Builders<Credential>.Filter.Eq(c => c.Id, credential.Id);
        await _credentials.ReplaceOneAsync(filter, credential);
    }

    public async Task AddCredentialToUserAsync(string userId, byte[] credentialId, byte[] publicKey, uint signCounter, string lastUsedPlatformInfo)
    {
        var credential = new Credential
        {
            Id = credentialId,
            PublicKey = publicKey,
            SignCounter = signCounter,
            LastUsedPlatformInfo = lastUsedPlatformInfo,
            UserId = userId
        };
        await _credentials.InsertOneAsync(credential);
    }

    public async Task<CredentialRevokeResult> RevokeCredentialAsync(string userId, byte[] credentialId)
    {
        var otherCredentialCount = await _credentials.AsQueryable()
            .CountAsync(credential => credential.UserId == userId && credential.Id != credentialId);

        if (otherCredentialCount > 0)
        {
            var filter = Builders<Credential>.Filter.Eq(c => c.UserId, userId) & Builders<Credential>.Filter.Eq(c => c.Id, credentialId);
            var deleteResult = await _credentials.DeleteOneAsync(filter);
            return deleteResult.DeletedCount > 0 ? CredentialRevokeResult.Success : CredentialRevokeResult.NotFound;
        }

        return CredentialRevokeResult.CannotRevokePrimary;
    }
}

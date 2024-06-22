using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using WebAPI.Models;

namespace WebAPI.Infrastructure;

public interface IUserRepository
{
    Task<User> CreateUserAsync(string userName);

    Task<User?> GetUserAsync(string userName);

    Task<User?> GetUserByIdAsync(string userId);

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

    public UserRepository(IPassKeysDbContext database)
    {
        _users = database.Collection<User>();
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

    public async Task<User?> GetUserByIdAsync(string userId)
    {
        var filterBuilder = Builders<User>.Filter;
        var filter = filterBuilder.Eq(u => u.Uuid, userId);

        return await _users.Find(filter).FirstOrDefaultAsync();
    }

    public async ValueTask<Credential?> GetCredentialAsync(byte[] credentialId)
    {
        var user = await _users.Find(u => u.Credentials.Any(c => c.Id == credentialId)).FirstOrDefaultAsync();
        return user?.Credentials.FirstOrDefault(c => c.Id == credentialId);
    }

    public async Task<bool> IsCredentialIdUniqueToUserAsync(byte[] userHandle, byte[] credentialId)
    {
        var userId = new Guid(userHandle).ToString();
        var user = await GetUserByIdAsync(userId);
        return user?.Credentials.All(c => c.Id != credentialId) ?? true;
    }

    public async Task<bool> VerifyCredentialOwnershipAsync(byte[] userHandle, byte[] credentialId)
    {
        var userId = new Guid(userHandle).ToString();
        var credential = await GetCredentialAsync(credentialId);
        return credential?.UserId == userId;
    }

    public async Task UpdateCredentialAsync(Credential credential)
    {
        var filter = Builders<User>.Filter.ElemMatch(u => u.Credentials, c => c.Id == credential.Id);
        var pullUpdate = Builders<User>.Update.PullFilter(u => u.Credentials, c => c.Id == credential.Id);
        var pushUpdate = Builders<User>.Update.Push(u => u.Credentials, credential);

        using var session = await _users.Database.Client.StartSessionAsync();
        session.StartTransaction();

        try
        {
            // Remove the old credential
            await _users.UpdateOneAsync(session, filter, pullUpdate);

            // Add the updated credential
            await _users.UpdateOneAsync(session, filter, pushUpdate);

            await session.CommitTransactionAsync();
        }
        catch
        {
            await session.AbortTransactionAsync();
            throw;
        }
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

        var filter = Builders<User>.Filter.Eq(u => u.Uuid, userId);
        var update = Builders<User>.Update.AddToSet(u => u.Credentials, credential);

        await _users.UpdateOneAsync(filter, update);
    }

    public async Task<CredentialRevokeResult> RevokeCredentialAsync(string userId, byte[] credentialId)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Uuid, userId);
        var update = Builders<User>.Update.PullFilter(u => u.Credentials, c => c.Id == credentialId);

        var user = await GetUserByIdAsync(userId);
        if (user == null)
        {
            return CredentialRevokeResult.NotFound;
        }

        var otherCredentialCount = user.Credentials.Count(c => c.Id != credentialId);

        if (otherCredentialCount > 0)
        {
            var result = await _users.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0 ? CredentialRevokeResult.Success : CredentialRevokeResult.NotFound;
        }

        return CredentialRevokeResult.CannotRevokePrimary;
    }
}

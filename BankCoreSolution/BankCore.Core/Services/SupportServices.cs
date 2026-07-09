using BankCore.Core.Interfaces;
using BankCore.Core.Models;
using System.Security.Cryptography;
using System.Text;

namespace BankCore.Core.Services;

public class PasswordHasher : IPasswordHasher
{
    public (string hash, string salt) HashPassword(string password)
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(32);
        string salt = Convert.ToBase64String(saltBytes);
        string hash = ComputeHash(password, salt);
        return (hash, salt);
    }

    public bool VerifyPassword(string password, string hash, string salt)
    {
        string computed = ComputeHash(password, salt);
        return computed == hash;
    }

    private static string ComputeHash(string password, string salt)
    {
        using var sha = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(password + salt);
        byte[] hashBytes = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}

public class AuditService : IAuditService
{
    private readonly IAuditRepository _repo;

    public AuditService(IAuditRepository repo)
    {
        _repo = repo;
    }

    public void Log(string eventType, string username, string description,
        string? reference = null, bool isSuccessful = true, string ipAddress = "127.0.0.1")
    {
        var log = new AuditLog
        {
            EventType    = eventType,
            Username     = username,
            Description  = description,
            RelatedReference = reference,
            IsSuccessful = isSuccessful,
            Timestamp    = DateTime.UtcNow,
            IpAddress    = ipAddress
        };
        _repo.Add(log);
    }
}

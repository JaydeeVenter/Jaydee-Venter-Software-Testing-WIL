using BankCore.Core.Interfaces;
using BankCore.Core.Models;

namespace BankCore.Core.Services;

/// <summary>
/// Manages user authentication, session management, and role-based access control.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository    _userRepo;
    private readonly ISessionRepository _sessionRepo;
    private readonly IPasswordHasher    _hasher;
    private readonly IAuditService      _audit;
    private readonly IValidationService _validator;

    private const int MaxFailedAttempts  = 3;
    private const int LockoutMinutes     = 30;
    private const int SessionTimeoutMins = 60;
    private const int PasswordHistoryLen = 5;

    private static readonly Dictionary<UserRole, HashSet<string>> _permissions = new()
    {
        [UserRole.Admin] = new HashSet<string>
        {
            "CREATE_ACCOUNT", "UPDATE_ACCOUNT", "CLOSE_ACCOUNT",
            "DEPOSIT", "WITHDRAW", "TRANSFER", "REVERSE_TRANSACTION",
            "APPROVE_LOAN", "REJECT_LOAN",
            "CREATE_USER", "LOCK_USER", "UNLOCK_USER",
            "VIEW_AUDIT_LOG", "GENERATE_REPORT", "VIEW_ALL_ACCOUNTS"
        },
        [UserRole.Manager] = new HashSet<string>
        {
            "CREATE_ACCOUNT", "UPDATE_ACCOUNT",
            "DEPOSIT", "WITHDRAW", "TRANSFER",
            "APPROVE_LOAN", "REJECT_LOAN",
            "VIEW_AUDIT_LOG", "GENERATE_REPORT", "VIEW_ALL_ACCOUNTS"
        },
        [UserRole.Teller] = new HashSet<string>
        {
            "DEPOSIT", "WITHDRAW", "TRANSFER",
            "VIEW_OWN_ACCOUNTS"
        },
        [UserRole.Auditor] = new HashSet<string>
        {
            "VIEW_AUDIT_LOG", "GENERATE_REPORT", "VIEW_ALL_ACCOUNTS"
        }
    };

    public AuthService(IUserRepository userRepo, ISessionRepository sessionRepo,
        IPasswordHasher hasher, IAuditService audit, IValidationService validator)
    {
        _userRepo    = userRepo;
        _sessionRepo = sessionRepo;
        _hasher      = hasher;
        _audit       = audit;
        _validator   = validator;
    }

    /// <summary>
    /// Authenticates a user and returns a session token.
    /// Accounts are locked after 3 failed attempts for 30 minutes.
    /// </summary>
    public OperationResult<Session> Login(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return OperationResult<Session>.Failure("Username and password are required.");

        var user = _userRepo.GetByUsername(username);
        if (user == null)
        {
            _audit.Log("LOGIN_FAILED", username, "User not found.", isSuccessful: false);
            return OperationResult<Session>.Failure("Invalid username or password.");
        }

        if (user.IsLocked)
        {
            // BUG-013: Lockout expiry is never checked. Once locked, user can never log in
            // regardless of LockoutExpiry. Should check: if (DateTime.UtcNow > user.LockoutExpiry) unlock first.
            _audit.Log("LOGIN_BLOCKED", username, "Account locked.", isSuccessful: false);
            return OperationResult<Session>.Failure("Account is locked. Contact your administrator.");
        }

        bool passwordValid = _hasher.VerifyPassword(password, user.PasswordHash, user.Salt);
        if (!passwordValid)
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.IsLocked      = true;
                user.LockoutExpiry = DateTime.UtcNow.AddMinutes(LockoutMinutes);
            }
            _userRepo.Update(user);
            _audit.Log("LOGIN_FAILED", username, "Invalid password.", isSuccessful: false);
            return OperationResult<Session>.Failure("Invalid username or password.");
        }

        // Successful login — reset counters
        user.FailedLoginAttempts = 0;
        user.LastLoginDate       = DateTime.UtcNow;
        _userRepo.Update(user);

        var session = new Session
        {
            Token     = GenerateToken(),
            UserId    = user.Id,
            Username  = user.Username,
            Role      = user.Role,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SessionTimeoutMins),
            IsActive  = true
        };

        _sessionRepo.Add(session);
        _audit.Log("LOGIN_SUCCESS", username, "User logged in.", session.Token);
        return OperationResult<Session>.Success(session, "Login successful.");
    }

    public OperationResult<bool> Logout(string token)
    {
        var session = _sessionRepo.GetByToken(token);
        if (session == null)
            return OperationResult<bool>.Failure("Session not found.");

        session.IsActive = false;
        _sessionRepo.Update(session);
        _audit.Log("LOGOUT", session.Username, "User logged out.", token);
        return OperationResult<bool>.Success(true, "Logged out successfully.");
    }

    /// <summary>
    /// Validates a session token. Returns failure if expired or inactive.
    /// </summary>
    public OperationResult<bool> ValidateSession(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return OperationResult<bool>.Failure("Token is required.");

        var session = _sessionRepo.GetByToken(token);
        if (session == null)
            return OperationResult<bool>.Failure("Invalid session.");

        // BUG-014: IsActive is never checked. Logged-out sessions still pass validation.
        if (DateTime.UtcNow > session.ExpiresAt)
            return OperationResult<bool>.Failure("Session has expired.");

        return OperationResult<bool>.Success(true);
    }

    public OperationResult<bool> ChangePassword(string token, string currentPassword,
        string newPassword)
    {
        var sessionResult = ValidateSession(token);
        if (!sessionResult.IsSuccess)
            return OperationResult<bool>.Failure(sessionResult.Message);

        var session = _sessionRepo.GetByToken(token)!;
        var user    = _userRepo.GetById(session.UserId);
        if (user == null) return OperationResult<bool>.Failure("User not found.");

        if (!_hasher.VerifyPassword(currentPassword, user.PasswordHash, user.Salt))
            return OperationResult<bool>.Failure("Current password is incorrect.");

        if (!_validator.IsValidPassword(newPassword))
            return OperationResult<bool>.Failure(
                "New password does not meet complexity requirements.");

        // Check password history
        (string newHash, string newSalt) = _hasher.HashPassword(newPassword);
        foreach (var oldHash in user.PasswordHistory)
        {
            // Simplified: store as hash only for history check
            if (oldHash == newHash)
                return OperationResult<bool>.Failure(
                    "New password cannot be the same as a recent password.");
        }

        user.PasswordHistory.Add(user.PasswordHash);
        if (user.PasswordHistory.Count > PasswordHistoryLen)
            user.PasswordHistory.RemoveAt(0);

        user.PasswordHash = newHash;
        user.Salt         = newSalt;
        _userRepo.Update(user);
        _audit.Log("PASSWORD_CHANGED", user.Username, "Password changed.");
        return OperationResult<bool>.Success(true, "Password changed successfully.");
    }

    public OperationResult<User> RegisterUser(string username, string password,
        UserRole role, string createdBy)
    {
        if (!_validator.IsValidUsername(username))
            return OperationResult<User>.Failure("Invalid username format.");

        if (!_validator.IsValidPassword(password))
            return OperationResult<User>.Failure("Password does not meet requirements.");

        if (_userRepo.UsernameExists(username))
            return OperationResult<User>.Failure("Username already exists.");

        (string hash, string salt) = _hasher.HashPassword(password);
        var user = new User
        {
            Username     = username,
            PasswordHash = hash,
            Salt         = salt,
            Role         = role,
            IsLocked     = false
        };

        _userRepo.Add(user);
        _audit.Log("USER_CREATED", createdBy, $"User {username} created with role {role}.");
        return OperationResult<User>.Success(user, "User created.");
    }

    public OperationResult<bool> LockUser(string username, string lockedBy)
    {
        var user = _userRepo.GetByUsername(username);
        if (user == null) return OperationResult<bool>.Failure("User not found.");
        user.IsLocked      = true;
        user.LockoutExpiry = DateTime.UtcNow.AddMinutes(LockoutMinutes);
        _userRepo.Update(user);
        _sessionRepo.InvalidateAllForUser(user.Id);
        _audit.Log("USER_LOCKED", lockedBy, $"User {username} locked.");
        return OperationResult<bool>.Success(true);
    }

    public OperationResult<bool> UnlockUser(string username, string unlockedBy)
    {
        var user = _userRepo.GetByUsername(username);
        if (user == null) return OperationResult<bool>.Failure("User not found.");
        user.IsLocked            = false;
        user.FailedLoginAttempts = 0;
        user.LockoutExpiry       = null;
        _userRepo.Update(user);
        _audit.Log("USER_UNLOCKED", unlockedBy, $"User {username} unlocked.");
        return OperationResult<bool>.Success(true);
    }

    /// <summary>
    /// Checks whether the session owner has permission for the requested operation.
    /// </summary>
    public bool HasPermission(string token, string operation)
    {
        var session = _sessionRepo.GetByToken(token);
        // BUG-015: Does not check if session is active or expired before granting permission.
        // An expired/logged-out session can still pass HasPermission checks.
        if (session == null) return false;
        return _permissions.TryGetValue(session.Role, out var perms) && perms.Contains(operation);
    }

    private static string GenerateToken()
        => $"BCT-{Guid.NewGuid():N}-{Guid.NewGuid().ToString("N")[..8]}".ToUpper();
}

using BankCore.Core.Interfaces;
using BankCore.Core.Models;

namespace BankCore.Core.Services;

/// <summary>
/// Manages the full lifecycle of bank accounts.
/// </summary>
public class AccountService : IAccountService
{
    private readonly IAccountRepository _accountRepo;
    private readonly IValidationService _validator;
    private readonly IAuditService _audit;

    private static int _accountCounter = 1000000000;

    public AccountService(IAccountRepository accountRepo,
        IValidationService validator, IAuditService audit)
    {
        _accountRepo = accountRepo;
        _validator   = validator;
        _audit       = audit;
    }

    /// <summary>
    /// Creates a new bank account with an initial deposit.
    /// Minimum deposits: Savings=R100, Current=R500, FixedDeposit=R1000, Notice=R500
    /// </summary>
    public OperationResult<Account> CreateAccount(string ownerName, string ownerIdNumber,
        AccountType type, decimal initialDeposit, string branchCode)
    {
        if (!_validator.IsValidName(ownerName))
            return OperationResult<Account>.Failure("Invalid owner name.");

        if (!_validator.IsValidSouthAfricanIdNumber(ownerIdNumber))
            return OperationResult<Account>.Failure("Invalid South African ID number.");

        if (!_validator.IsValidBranchCode(branchCode))
            return OperationResult<Account>.Failure("Invalid branch code.");

        decimal minDeposit = type switch
        {
            AccountType.Savings      => 100m,
            AccountType.Current      => 500m,
            AccountType.FixedDeposit => 1000m,
            AccountType.Notice       => 500m,
            _                        => 100m
        };

        if (initialDeposit < minDeposit)
            return OperationResult<Account>.Failure(
                $"Minimum opening deposit for {type} account is R{minDeposit:F2}.");

        var account = new Account
        {
            AccountNumber        = GenerateAccountNumber(),
            OwnerName            = ownerName.Trim(),
            OwnerIdNumber        = ownerIdNumber,
            Type                 = type,
            Status               = AccountStatus.Active,
            Balance              = initialDeposit,
            // BUG-005: Daily withdrawal limit is set to initial deposit instead of type-based limit
            DailyWithdrawalLimit = initialDeposit,
            DateOpened           = DateTime.UtcNow,
            LastActivityDate     = DateTime.UtcNow,
            BranchCode           = branchCode
        };

        _accountRepo.Add(account);
        _audit.Log("ACCOUNT_CREATED", "SYSTEM", $"Account {account.AccountNumber} created for {ownerName}",
            account.AccountNumber);

        return OperationResult<Account>.Success(account, "Account created successfully.");
    }

    public OperationResult<Account> UpdateAccount(int accountId, string ownerName, string branchCode)
    {
        var account = _accountRepo.GetById(accountId);
        if (account == null)
            return OperationResult<Account>.Failure("Account not found.");

        if (account.Status == AccountStatus.Closed)
            return OperationResult<Account>.Failure("Cannot update a closed account.");

        if (!_validator.IsValidName(ownerName))
            return OperationResult<Account>.Failure("Invalid owner name.");

        if (!_validator.IsValidBranchCode(branchCode))
            return OperationResult<Account>.Failure("Invalid branch code.");

        account.OwnerName  = ownerName.Trim();
        account.BranchCode = branchCode;
        _accountRepo.Update(account);
        _audit.Log("ACCOUNT_UPDATED", "SYSTEM", $"Account {account.AccountNumber} updated.", account.AccountNumber);

        return OperationResult<Account>.Success(account, "Account updated.");
    }

    /// <summary>
    /// Closes an account. Balance must be zero before closure.
    /// </summary>
    public OperationResult<Account> CloseAccount(int accountId, string requestedBy)
    {
        var account = _accountRepo.GetById(accountId);
        if (account == null)
            return OperationResult<Account>.Failure("Account not found.");

        if (account.Status == AccountStatus.Closed)
            return OperationResult<Account>.Failure("Account is already closed.");

        // BUG-006: checks > 0 instead of != 0, so accounts with negative balance can be closed
        if (account.Balance > 0)
            return OperationResult<Account>.Failure(
                "Account balance must be zero before closure. Please withdraw remaining funds.");

        account.Status      = AccountStatus.Closed;
        account.DateClosed  = DateTime.UtcNow;
        _accountRepo.Update(account);
        _audit.Log("ACCOUNT_CLOSED", requestedBy, $"Account {account.AccountNumber} closed.", account.AccountNumber);

        return OperationResult<Account>.Success(account, "Account closed successfully.");
    }

    public OperationResult<Account> GetAccount(int accountId)
    {
        var account = _accountRepo.GetById(accountId);
        return account == null
            ? OperationResult<Account>.Failure("Account not found.")
            : OperationResult<Account>.Success(account);
    }

    public OperationResult<Account> GetAccountByNumber(string accountNumber)
    {
        if (!_validator.IsValidAccountNumber(accountNumber))
            return OperationResult<Account>.Failure("Invalid account number format.");

        var account = _accountRepo.GetByAccountNumber(accountNumber);
        return account == null
            ? OperationResult<Account>.Failure("Account not found.")
            : OperationResult<Account>.Success(account);
    }

    public OperationResult<List<Account>> GetAccountsByOwner(string idNumber)
    {
        if (!_validator.IsValidSouthAfricanIdNumber(idNumber))
            return OperationResult<List<Account>>.Failure("Invalid ID number.");

        var accounts = _accountRepo.GetByOwnerIdNumber(idNumber);
        return OperationResult<List<Account>>.Success(accounts);
    }

    public OperationResult<bool> SetDormant(int accountId)
    {
        var account = _accountRepo.GetById(accountId);
        if (account == null) return OperationResult<bool>.Failure("Account not found.");
        if (account.Status != AccountStatus.Active)
            return OperationResult<bool>.Failure("Only active accounts can be set dormant.");

        account.Status = AccountStatus.Dormant;
        _accountRepo.Update(account);
        _audit.Log("ACCOUNT_DORMANT", "SYSTEM", $"Account {account.AccountNumber} set to dormant.");
        return OperationResult<bool>.Success(true);
    }

    public OperationResult<bool> ReactivateAccount(int accountId, decimal reactivationDeposit)
    {
        var account = _accountRepo.GetById(accountId);
        if (account == null) return OperationResult<bool>.Failure("Account not found.");
        if (account.Status != AccountStatus.Dormant)
            return OperationResult<bool>.Failure("Only dormant accounts can be reactivated.");
        if (reactivationDeposit < 50m)
            return OperationResult<bool>.Failure("Reactivation requires a minimum deposit of R50.");

        account.Status           = AccountStatus.Active;
        account.Balance         += reactivationDeposit;
        account.LastActivityDate = DateTime.UtcNow;
        _accountRepo.Update(account);
        _audit.Log("ACCOUNT_REACTIVATED", "SYSTEM", $"Account {account.AccountNumber} reactivated.");
        return OperationResult<bool>.Success(true);
    }

    private string GenerateAccountNumber()
    {
        return "BC" + Interlocked.Increment(ref _accountCounter).ToString();
    }
}

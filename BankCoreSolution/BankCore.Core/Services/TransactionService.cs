using BankCore.Core.Interfaces;
using BankCore.Core.Models;

namespace BankCore.Core.Services;

/// <summary>
/// Handles all financial transactions: deposits, withdrawals, transfers, reversals.
/// All monetary values are in ZAR (South African Rand).
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly IAccountRepository  _accountRepo;
    private readonly ITransactionRepository _txnRepo;
    private readonly IValidationService  _validator;
    private readonly IAuditService       _audit;

    private const decimal MaxSingleTransaction = 50000m;
    private const int     ReversalWindowHours  = 24;

    public TransactionService(IAccountRepository accountRepo,
        ITransactionRepository txnRepo,
        IValidationService validator,
        IAuditService audit)
    {
        _accountRepo = accountRepo;
        _txnRepo     = txnRepo;
        _validator   = validator;
        _audit       = audit;
    }

    /// <summary>
    /// Deposits funds into an account.
    /// Account must be Active. Amount must be between R0.01 and R50,000.
    /// </summary>
    public OperationResult<Transaction> Deposit(int accountId, decimal amount,
        string description, string processedBy)
    {
        if (amount <= 0)
            return OperationResult<Transaction>.Failure("Deposit amount must be greater than zero.");

        if (amount > MaxSingleTransaction)
            return OperationResult<Transaction>.Failure(
                $"Single deposit cannot exceed R{MaxSingleTransaction:F2}.");

        var account = _accountRepo.GetById(accountId);
        if (account == null)
            return OperationResult<Transaction>.Failure("Account not found.");

        if (account.Status != AccountStatus.Active)
            return OperationResult<Transaction>.Failure(
                "Deposits can only be made to Active accounts.");

        decimal balanceBefore = account.Balance;
        account.Balance          += amount;
        account.LastActivityDate  = DateTime.UtcNow;
        _accountRepo.Update(account);

        var txn = CreateTransaction(accountId, null, TransactionType.Deposit,
            amount, balanceBefore, account.Balance, description, processedBy);

        _txnRepo.Add(txn);
        _audit.Log("DEPOSIT", processedBy, $"Deposit of R{amount:F2} to {account.AccountNumber}",
            txn.TransactionReference);

        return OperationResult<Transaction>.Success(txn, "Deposit successful.");
    }

    /// <summary>
    /// Withdraws funds from an account.
    /// Rules: Account Active, sufficient balance, within daily limit, amount <= R50,000.
    /// </summary>
    public OperationResult<Transaction> Withdraw(int accountId, decimal amount,
        string description, string processedBy)
    {
        if (amount <= 0)
            return OperationResult<Transaction>.Failure("Withdrawal amount must be greater than zero.");

        if (amount > MaxSingleTransaction)
            return OperationResult<Transaction>.Failure(
                $"Single withdrawal cannot exceed R{MaxSingleTransaction:F2}.");

        var account = _accountRepo.GetById(accountId);
        if (account == null)
            return OperationResult<Transaction>.Failure("Account not found.");

        if (account.Status != AccountStatus.Active)
            return OperationResult<Transaction>.Failure(
                "Withdrawals can only be made from Active accounts.");

        // BUG-007: insufficient funds check uses < instead of <=
        // Allows withdrawal of exactly the full balance (balance becomes 0 — correct)
        // BUT for Current accounts a negative balance should be blocked.
        // More critically: the daily limit check below is performed AFTER the balance check
        // but the daily total is never actually reset between days (no date comparison).
        if (account.Balance < amount)
            return OperationResult<Transaction>.Failure("Insufficient funds.");

        // BUG-008: Daily limit check compares withdrawn TODAY against the limit
        // but DailyWithdrawnToday is NEVER reset at day boundary — it accumulates forever
        if (account.DailyWithdrawnToday + amount > account.DailyWithdrawalLimit)
            return OperationResult<Transaction>.Failure(
                $"Daily withdrawal limit of R{account.DailyWithdrawalLimit:F2} would be exceeded.");

        decimal balanceBefore = account.Balance;
        account.Balance              -= amount;
        account.DailyWithdrawnToday  += amount;
        account.LastActivityDate      = DateTime.UtcNow;
        _accountRepo.Update(account);

        var txn = CreateTransaction(accountId, null, TransactionType.Withdrawal,
            amount, balanceBefore, account.Balance, description, processedBy);
        _txnRepo.Add(txn);
        _audit.Log("WITHDRAWAL", processedBy, $"Withdrawal of R{amount:F2} from {account.AccountNumber}",
            txn.TransactionReference);

        return OperationResult<Transaction>.Success(txn, "Withdrawal successful.");
    }

    /// <summary>
    /// Transfers funds between two accounts atomically.
    /// Both accounts must be Active. Transfer is atomic — if credit fails, debit is rolled back.
    /// </summary>
    public OperationResult<Transaction> Transfer(int fromAccountId, int toAccountId,
        decimal amount, string description, string processedBy)
    {
        if (fromAccountId == toAccountId)
            return OperationResult<Transaction>.Failure("Cannot transfer to the same account.");

        if (amount <= 0)
            return OperationResult<Transaction>.Failure("Transfer amount must be greater than zero.");

        if (amount > MaxSingleTransaction)
            return OperationResult<Transaction>.Failure(
                $"Single transfer cannot exceed R{MaxSingleTransaction:F2}.");

        var fromAccount = _accountRepo.GetById(fromAccountId);
        if (fromAccount == null)
            return OperationResult<Transaction>.Failure("Source account not found.");

        var toAccount = _accountRepo.GetById(toAccountId);
        if (toAccount == null)
            return OperationResult<Transaction>.Failure("Destination account not found.");

        if (fromAccount.Status != AccountStatus.Active)
            return OperationResult<Transaction>.Failure("Source account is not Active.");

        // BUG-009: Does NOT check if destination account is Active.
        // Transfers can be made to Dormant or Closed accounts.

        if (fromAccount.Balance < amount)
            return OperationResult<Transaction>.Failure("Insufficient funds in source account.");

        decimal fromBefore = fromAccount.Balance;
        decimal toBefore   = toAccount.Balance;

        fromAccount.Balance          -= amount;
        fromAccount.DailyWithdrawnToday += amount;
        fromAccount.LastActivityDate  = DateTime.UtcNow;
        toAccount.Balance            += amount;
        toAccount.LastActivityDate    = DateTime.UtcNow;

        _accountRepo.Update(fromAccount);
        _accountRepo.Update(toAccount);

        var txn = CreateTransaction(fromAccountId, toAccountId, TransactionType.Transfer,
            amount, fromBefore, fromAccount.Balance, description, processedBy);
        _txnRepo.Add(txn);

        _audit.Log("TRANSFER", processedBy,
            $"Transfer of R{amount:F2} from {fromAccount.AccountNumber} to {toAccount.AccountNumber}",
            txn.TransactionReference);

        return OperationResult<Transaction>.Success(txn, "Transfer successful.");
    }

    /// <summary>
    /// Reverses a transaction. Only Completed transactions within the reversal window can be reversed.
    /// </summary>
    public OperationResult<Transaction> ReverseTransaction(string transactionReference,
        string reason, string processedBy)
    {
        if (string.IsNullOrWhiteSpace(transactionReference))
            return OperationResult<Transaction>.Failure("Transaction reference is required.");

        var original = _txnRepo.GetByReference(transactionReference);
        if (original == null)
            return OperationResult<Transaction>.Failure("Transaction not found.");

        if (original.Status == TransactionStatus.Reversed)
            return OperationResult<Transaction>.Failure("Transaction has already been reversed.");

        if (original.Status != TransactionStatus.Completed)
            return OperationResult<Transaction>.Failure("Only Completed transactions can be reversed.");

        var ageHours = (DateTime.UtcNow - original.Timestamp).TotalHours;
        if (ageHours > ReversalWindowHours)
            return OperationResult<Transaction>.Failure(
                $"Reversal window of {ReversalWindowHours} hours has expired.");

        var account = _accountRepo.GetById(original.AccountId);
        if (account == null)
            return OperationResult<Transaction>.Failure("Associated account not found.");

        decimal balanceBefore = account.Balance;

        if (original.Type == TransactionType.Deposit || original.Type == TransactionType.Transfer)
        {
            if (account.Balance < original.Amount)
                return OperationResult<Transaction>.Failure(
                    "Insufficient funds to reverse this deposit.");
            account.Balance -= original.Amount;
        }
        else if (original.Type == TransactionType.Withdrawal)
        {
            account.Balance += original.Amount;
        }

        account.LastActivityDate = DateTime.UtcNow;
        _accountRepo.Update(account);

        original.Status           = TransactionStatus.Reversed;
        original.ReversalReference = $"REV-{transactionReference}";
        _txnRepo.Update(original);

        var reversalTxn = CreateTransaction(original.AccountId, null, TransactionType.Reversal,
            original.Amount, balanceBefore, account.Balance,
            $"Reversal of {transactionReference}: {reason}", processedBy);
        reversalTxn.ReversalReference = transactionReference;
        _txnRepo.Add(reversalTxn);

        _audit.Log("REVERSAL", processedBy,
            $"Transaction {transactionReference} reversed. Reason: {reason}",
            reversalTxn.TransactionReference);

        return OperationResult<Transaction>.Success(reversalTxn, "Transaction reversed successfully.");
    }

    public OperationResult<List<Transaction>> GetTransactionHistory(int accountId,
        DateTime? from = null, DateTime? to = null)
    {
        var account = _accountRepo.GetById(accountId);
        if (account == null)
            return OperationResult<List<Transaction>>.Failure("Account not found.");

        List<Transaction> txns;
        if (from.HasValue && to.HasValue)
            txns = _txnRepo.GetByDateRange(accountId, from.Value, to.Value);
        else
            txns = _txnRepo.GetByAccountId(accountId);

        return OperationResult<List<Transaction>>.Success(txns);
    }

    private Transaction CreateTransaction(int accountId, int? targetId,
        TransactionType type, decimal amount, decimal before, decimal after,
        string description, string processedBy)
    {
        return new Transaction
        {
            TransactionReference = $"TXN-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            AccountId            = accountId,
            TargetAccountId      = targetId,
            Type                 = type,
            Status               = TransactionStatus.Completed,
            Amount               = amount,
            BalanceBefore        = before,
            BalanceAfter         = after,
            Description          = description,
            Timestamp            = DateTime.UtcNow,
            ProcessedBy          = processedBy
        };
    }
}

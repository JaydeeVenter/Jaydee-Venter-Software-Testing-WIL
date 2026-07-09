namespace BankCore.Core.Models;

public enum AccountType { Savings, Current, FixedDeposit, Notice }
public enum AccountStatus { Pending, Active, Dormant, Closed }
public enum TransactionType { Deposit, Withdrawal, Transfer, Reversal }
public enum TransactionStatus { Pending, Completed, Failed, Reversed }
public enum LoanStatus { Pending, Approved, Active, Arrears, Settled, WrittenOff, Rejected }
public enum LoanType { Personal, Home, Vehicle, Business }
public enum UserRole { Admin, Manager, Teller, Auditor }

public class Account
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerIdNumber { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public AccountStatus Status { get; set; }
    public decimal Balance { get; set; }
    public decimal DailyWithdrawalLimit { get; set; }
    public decimal DailyWithdrawnToday { get; set; }
    public DateTime DateOpened { get; set; }
    public DateTime? DateClosed { get; set; }
    public DateTime LastActivityDate { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public List<Transaction> Transactions { get; set; } = new();
}

public class Transaction
{
    public int Id { get; set; }
    public string TransactionReference { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public int? TargetAccountId { get; set; }
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string ProcessedBy { get; set; } = string.Empty;
    public string? ReversalReference { get; set; }
}

public class Loan
{
    public int Id { get; set; }
    public string LoanReference { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public LoanType Type { get; set; }
    public LoanStatus Status { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal InterestRate { get; set; }   // annual, e.g. 0.12 = 12%
    public int TermMonths { get; set; }
    public decimal MonthlyInstalment { get; set; }
    public int CreditScore { get; set; }
    public decimal ApplicantMonthlyIncome { get; set; }
    public decimal TotalDebtObligations { get; set; }
    public DateTime ApplicationDate { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public DateTime? DisbursementDate { get; set; }
    public DateTime? SettlementDate { get; set; }
    public int MissedPayments { get; set; }
    public decimal ArrearsAmount { get; set; }
    public List<LoanRepayment> RepaymentSchedule { get; set; } = new();
}

public class LoanRepayment
{
    public int InstallmentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal InstallmentAmount { get; set; }
    public decimal PrincipalPortion { get; set; }
    public decimal InterestPortion { get; set; }
    public decimal ClosingBalance { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsLocked { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public DateTime? LockoutExpiry { get; set; }
    public List<string> PasswordHistory { get; set; } = new();
}

public class Session
{
    public string Token { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? RelatedReference { get; set; }
    public DateTime Timestamp { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
}

public class OperationResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();

    public static OperationResult<T> Success(T data, string message = "")
        => new() { IsSuccess = true, Data = data, Message = message };

    public static OperationResult<T> Failure(string error)
        => new() { IsSuccess = false, Message = error, Errors = new List<string> { error } };
}

public class AccountStatement
{
    public Account Account { get; set; } = null!;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal TotalDebits { get; set; }
    public List<Transaction> Transactions { get; set; } = new();
}

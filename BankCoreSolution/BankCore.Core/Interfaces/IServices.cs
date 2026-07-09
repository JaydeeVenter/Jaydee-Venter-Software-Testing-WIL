using BankCore.Core.Models;

namespace BankCore.Core.Interfaces;

public interface IAccountRepository
{
    Account? GetById(int id);
    Account? GetByAccountNumber(string accountNumber);
    List<Account> GetAll();
    List<Account> GetByOwnerIdNumber(string idNumber);
    void Add(Account account);
    void Update(Account account);
    void Delete(int id);
    bool AccountNumberExists(string accountNumber);
}

public interface ITransactionRepository
{
    Transaction? GetById(int id);
    Transaction? GetByReference(string reference);
    List<Transaction> GetByAccountId(int accountId);
    List<Transaction> GetByDateRange(int accountId, DateTime from, DateTime to);
    void Add(Transaction transaction);
    void Update(Transaction transaction);
    bool ReferenceExists(string reference);
}

public interface ILoanRepository
{
    Loan? GetById(int id);
    Loan? GetByReference(string reference);
    List<Loan> GetByAccountId(int accountId);
    List<Loan> GetByStatus(LoanStatus status);
    void Add(Loan loan);
    void Update(Loan loan);
}

public interface IUserRepository
{
    User? GetById(int id);
    User? GetByUsername(string username);
    List<User> GetAll();
    void Add(User user);
    void Update(User user);
    bool UsernameExists(string username);
}

public interface IAuditRepository
{
    void Add(AuditLog log);
    List<AuditLog> GetAll();
    List<AuditLog> GetByUsername(string username);
    List<AuditLog> GetByDateRange(DateTime from, DateTime to);
}

public interface ISessionRepository
{
    void Add(Session session);
    Session? GetByToken(string token);
    void Update(Session session);
    void InvalidateAllForUser(int userId);
}

public interface IAccountService
{
    OperationResult<Account> CreateAccount(string ownerName, string ownerIdNumber,
        AccountType type, decimal initialDeposit, string branchCode);
    OperationResult<Account> UpdateAccount(int accountId, string ownerName, string branchCode);
    OperationResult<Account> CloseAccount(int accountId, string requestedBy);
    OperationResult<Account> GetAccount(int accountId);
    OperationResult<Account> GetAccountByNumber(string accountNumber);
    OperationResult<List<Account>> GetAccountsByOwner(string idNumber);
    OperationResult<bool> SetDormant(int accountId);
    OperationResult<bool> ReactivateAccount(int accountId, decimal reactivationDeposit);
}

public interface ITransactionService
{
    OperationResult<Transaction> Deposit(int accountId, decimal amount,
        string description, string processedBy);
    OperationResult<Transaction> Withdraw(int accountId, decimal amount,
        string description, string processedBy);
    OperationResult<Transaction> Transfer(int fromAccountId, int toAccountId,
        decimal amount, string description, string processedBy);
    OperationResult<Transaction> ReverseTransaction(string transactionReference,
        string reason, string processedBy);
    OperationResult<List<Transaction>> GetTransactionHistory(int accountId,
        DateTime? from = null, DateTime? to = null);
}

public interface IInterestCalculator
{
    decimal SimpleInterest(decimal principal, decimal annualRate, int months);
    decimal CompoundInterest(decimal principal, decimal annualRate, int months,
        int compoundingFrequency);
    decimal DailyInterest(decimal principal, decimal annualRate, int days);
    decimal EffectiveAnnualRate(decimal nominalRate, int compoundingFrequency);
    decimal FutureValue(decimal principal, decimal annualRate, int months,
        bool isCompound = true);
}

public interface ILoanService
{
    OperationResult<Loan> ApplyForLoan(int accountId, LoanType type, decimal amount,
        int termMonths, decimal annualRate, decimal monthlyIncome, decimal existingDebt,
        int creditScore);
    OperationResult<Loan> ApproveLoan(string loanReference, string approvedBy);
    OperationResult<Loan> RejectLoan(string loanReference, string reason, string rejectedBy);
    OperationResult<List<LoanRepayment>> GenerateRepaymentSchedule(string loanReference);
    OperationResult<Loan> ProcessRepayment(string loanReference, decimal amount,
        string processedBy);
    OperationResult<decimal> CalculateSettlementAmount(string loanReference);
    OperationResult<Loan> SettleLoan(string loanReference, string processedBy);
    OperationResult<Loan> GetLoan(string loanReference);
}

public interface IAuthService
{
    OperationResult<Session> Login(string username, string password);
    OperationResult<bool> Logout(string token);
    OperationResult<bool> ValidateSession(string token);
    OperationResult<bool> ChangePassword(string token, string currentPassword,
        string newPassword);
    OperationResult<User> RegisterUser(string username, string password, UserRole role,
        string createdBy);
    OperationResult<bool> LockUser(string username, string lockedBy);
    OperationResult<bool> UnlockUser(string username, string unlockedBy);
    bool HasPermission(string token, string operation);
}

public interface IReportingService
{
    OperationResult<AccountStatement> GenerateStatement(int accountId,
        DateTime from, DateTime to);
    OperationResult<List<AuditLog>> GetAuditLog(DateTime from, DateTime to);
    OperationResult<string> GenerateSummaryReport(DateTime reportDate);
}

public interface IValidationService
{
    bool IsValidSouthAfricanIdNumber(string idNumber);
    bool IsValidAccountNumber(string accountNumber);
    bool IsValidAmount(decimal amount, decimal min = 0.01m, decimal max = 999999.99m);
    bool IsValidName(string name);
    bool IsValidUsername(string username);
    bool IsValidPassword(string password);
    bool IsValidBranchCode(string branchCode);
    bool IsValidEmail(string email);
    bool IsSafeInput(string input);
}

public interface IPasswordHasher
{
    (string hash, string salt) HashPassword(string password);
    bool VerifyPassword(string password, string hash, string salt);
}

public interface IAuditService
{
    void Log(string eventType, string username, string description,
        string? reference = null, bool isSuccessful = true, string ipAddress = "127.0.0.1");
}

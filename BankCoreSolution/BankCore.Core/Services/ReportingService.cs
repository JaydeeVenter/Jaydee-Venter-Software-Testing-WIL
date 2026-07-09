using BankCore.Core.Interfaces;
using BankCore.Core.Models;
using System.Text;

namespace BankCore.Core.Services;

/// <summary>
/// Generates account statements, audit reports, and system summaries.
/// </summary>
public class ReportingService : IReportingService
{
    private readonly IAccountRepository     _accountRepo;
    private readonly ITransactionRepository _txnRepo;
    private readonly IAuditRepository       _auditRepo;

    public ReportingService(IAccountRepository accountRepo,
        ITransactionRepository txnRepo, IAuditRepository auditRepo)
    {
        _accountRepo = accountRepo;
        _txnRepo     = txnRepo;
        _auditRepo   = auditRepo;
    }

    /// <summary>
    /// Generates an account statement for the specified date range.
    /// Opening balance = balance before the first transaction in range.
    /// </summary>
    public OperationResult<AccountStatement> GenerateStatement(int accountId,
        DateTime from, DateTime to)
    {
        var account = _accountRepo.GetById(accountId);
        if (account == null)
            return OperationResult<AccountStatement>.Failure("Account not found.");

        if (account.Status == AccountStatus.Pending)
            return OperationResult<AccountStatement>.Failure("Account is not yet active.");

        if (from > to)
            return OperationResult<AccountStatement>.Failure(
                "From date cannot be later than To date.");

        var txns = _txnRepo.GetByDateRange(accountId, from, to.AddDays(1).AddSeconds(-1));

        decimal totalCredits = txns
            .Where(t => t.Type == TransactionType.Deposit ||
                        (t.Type == TransactionType.Transfer && t.TargetAccountId == accountId) ||
                        t.Type == TransactionType.Reversal)
            .Sum(t => t.Amount);

        decimal totalDebits = txns
            .Where(t => t.Type == TransactionType.Withdrawal ||
                        (t.Type == TransactionType.Transfer && t.AccountId == accountId))
            .Sum(t => t.Amount);

        decimal openingBalance = txns.Any()
            ? txns.OrderBy(t => t.Timestamp).First().BalanceBefore
            : account.Balance;

        var statement = new AccountStatement
        {
            Account        = account,
            FromDate       = from,
            ToDate         = to,
            OpeningBalance = openingBalance,
            ClosingBalance = openingBalance + totalCredits - totalDebits,
            TotalCredits   = totalCredits,
            TotalDebits    = totalDebits,
            Transactions   = txns.OrderBy(t => t.Timestamp).ToList()
        };

        return OperationResult<AccountStatement>.Success(statement);
    }

    public OperationResult<List<AuditLog>> GetAuditLog(DateTime from, DateTime to)
    {
        if (from > to)
            return OperationResult<List<AuditLog>>.Failure("Invalid date range.");

        var logs = _auditRepo.GetByDateRange(from, to);
        return OperationResult<List<AuditLog>>.Success(logs);
    }

    public OperationResult<string> GenerateSummaryReport(DateTime reportDate)
    {
        var allAccounts = _accountRepo.GetAll();

        int totalAccounts  = allAccounts.Count;
        int activeAccounts = allAccounts.Count(a => a.Status == AccountStatus.Active);
        int dormant        = allAccounts.Count(a => a.Status == AccountStatus.Dormant);
        int closed         = allAccounts.Count(a => a.Status == AccountStatus.Closed);
        decimal totalFunds = allAccounts
            .Where(a => a.Status != AccountStatus.Closed)
            .Sum(a => a.Balance);

        var sb = new StringBuilder();
        sb.AppendLine($"BANKCORE DAILY SUMMARY — {reportDate:yyyy-MM-dd}");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine($"Total Accounts  : {totalAccounts}");
        sb.AppendLine($"Active          : {activeAccounts}");
        sb.AppendLine($"Dormant         : {dormant}");
        sb.AppendLine($"Closed          : {closed}");
        sb.AppendLine($"Total Funds     : R{totalFunds:N2}");
        sb.AppendLine(new string('=', 50));

        return OperationResult<string>.Success(sb.ToString());
    }
}

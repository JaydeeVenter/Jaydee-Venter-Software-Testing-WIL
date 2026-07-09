using BankCore.Core.Interfaces;
using BankCore.Core.Models;

namespace BankCore.Core.Services;

/// <summary>
/// Handles loan applications, approvals, repayment schedules, and settlement.
/// </summary>
public class LoanService : ILoanService
{
    private readonly ILoanRepository    _loanRepo;
    private readonly IAccountRepository _accountRepo;
    private readonly IAuditService      _audit;

    private const decimal MaxDebtToIncomeRatio  = 0.40m;  // 40%
    private const int     MinCreditScore        = 600;
    private const decimal EarlySettlementFee    = 0.015m; // 1.5% of outstanding balance
    private const decimal ArrearsMonthlyPenalty = 0.02m;  // 2% of missed instalment

    public LoanService(ILoanRepository loanRepo, IAccountRepository accountRepo,
        IAuditService audit)
    {
        _loanRepo    = loanRepo;
        _accountRepo = accountRepo;
        _audit       = audit;
    }

    /// <summary>
    /// Processes a loan application.
    /// Eligibility: credit score >= 600, debt-to-income ratio <= 40%, account must be Active.
    /// </summary>
    public OperationResult<Loan> ApplyForLoan(int accountId, LoanType type, decimal amount,
        int termMonths, decimal annualRate, decimal monthlyIncome, decimal existingDebt,
        int creditScore)
    {
        var account = _accountRepo.GetById(accountId);
        if (account == null)
            return OperationResult<Loan>.Failure("Account not found.");

        if (account.Status != AccountStatus.Active)
            return OperationResult<Loan>.Failure("Loan applications require an Active account.");

        if (amount <= 0)
            return OperationResult<Loan>.Failure("Loan amount must be positive.");

        if (termMonths < 3 || termMonths > 360)
            return OperationResult<Loan>.Failure("Loan term must be between 3 and 360 months.");

        if (annualRate <= 0 || annualRate > 0.40m)
            return OperationResult<Loan>.Failure("Interest rate must be between 0.01% and 40%.");

        if (creditScore < MinCreditScore)
            return OperationResult<Loan>.Failure(
                $"Minimum credit score of {MinCreditScore} required. Applicant score: {creditScore}.");

        // BUG-010: DTI uses monthly income but existingDebt is treated as monthly obligations.
        // The bug: divides existingDebt by monthlyIncome instead of (existingDebt + newInstalment) / income.
        // New instalment is never factored into the DTI calculation.
        decimal monthlyInstalment = CalculatePMT(amount, annualRate, termMonths);
        decimal dti = existingDebt / monthlyIncome;   // missing: should be (existingDebt + monthlyInstalment) / monthlyIncome

        if (dti > MaxDebtToIncomeRatio)
            return OperationResult<Loan>.Failure(
                $"Debt-to-income ratio of {dti:P1} exceeds maximum allowed {MaxDebtToIncomeRatio:P0}.");

        var loan = new Loan
        {
            LoanReference          = $"LN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            AccountId              = accountId,
            Type                   = type,
            Status                 = LoanStatus.Pending,
            PrincipalAmount        = amount,
            OutstandingBalance     = amount,
            InterestRate           = annualRate,
            TermMonths             = termMonths,
            MonthlyInstalment      = monthlyInstalment,
            CreditScore            = creditScore,
            ApplicantMonthlyIncome = monthlyIncome,
            TotalDebtObligations   = existingDebt,
            ApplicationDate        = DateTime.UtcNow
        };

        _loanRepo.Add(loan);
        _audit.Log("LOAN_APPLICATION", "SYSTEM",
            $"Loan application {loan.LoanReference} for R{amount:F2}", loan.LoanReference);

        return OperationResult<Loan>.Success(loan, "Loan application submitted.");
    }

    public OperationResult<Loan> ApproveLoan(string loanReference, string approvedBy)
    {
        var loan = _loanRepo.GetByReference(loanReference);
        if (loan == null) return OperationResult<Loan>.Failure("Loan not found.");
        if (loan.Status != LoanStatus.Pending)
            return OperationResult<Loan>.Failure("Only Pending loans can be approved.");

        loan.Status           = LoanStatus.Approved;
        loan.ApprovalDate     = DateTime.UtcNow;
        loan.DisbursementDate = DateTime.UtcNow;
        loan.RepaymentSchedule = BuildRepaymentSchedule(loan);
        _loanRepo.Update(loan);

        // Credit the loan amount to the account
        var account = _accountRepo.GetById(loan.AccountId);
        if (account != null)
        {
            account.Balance += loan.PrincipalAmount;
            _accountRepo.Update(account);
        }

        loan.Status = LoanStatus.Active;
        _loanRepo.Update(loan);

        _audit.Log("LOAN_APPROVED", approvedBy, $"Loan {loanReference} approved.", loanReference);
        return OperationResult<Loan>.Success(loan, "Loan approved and disbursed.");
    }

    public OperationResult<Loan> RejectLoan(string loanReference, string reason, string rejectedBy)
    {
        var loan = _loanRepo.GetByReference(loanReference);
        if (loan == null) return OperationResult<Loan>.Failure("Loan not found.");
        if (loan.Status != LoanStatus.Pending)
            return OperationResult<Loan>.Failure("Only Pending loans can be rejected.");

        loan.Status = LoanStatus.Rejected;
        _loanRepo.Update(loan);
        _audit.Log("LOAN_REJECTED", rejectedBy, $"Loan {loanReference} rejected: {reason}", loanReference);
        return OperationResult<Loan>.Success(loan, "Loan rejected.");
    }

    public OperationResult<List<LoanRepayment>> GenerateRepaymentSchedule(string loanReference)
    {
        var loan = _loanRepo.GetByReference(loanReference);
        if (loan == null) return OperationResult<List<LoanRepayment>>.Failure("Loan not found.");
        if (loan.Status == LoanStatus.Pending || loan.Status == LoanStatus.Rejected)
            return OperationResult<List<LoanRepayment>>.Failure("Repayment schedule only available for approved loans.");

        return OperationResult<List<LoanRepayment>>.Success(loan.RepaymentSchedule);
    }

    /// <summary>
    /// Processes a repayment against the loan's outstanding balance.
    /// </summary>
    public OperationResult<Loan> ProcessRepayment(string loanReference, decimal amount,
        string processedBy)
    {
        var loan = _loanRepo.GetByReference(loanReference);
        if (loan == null) return OperationResult<Loan>.Failure("Loan not found.");
        if (loan.Status != LoanStatus.Active && loan.Status != LoanStatus.Arrears)
            return OperationResult<Loan>.Failure("Repayments can only be processed on Active or Arrears loans.");

        if (amount <= 0)
            return OperationResult<Loan>.Failure("Repayment amount must be positive.");

        // BUG-011: When repayment exceeds outstanding balance, the balance goes negative
        // instead of being capped at zero. Should check: if (amount > loan.OutstandingBalance) amount = loan.OutstandingBalance;
        loan.OutstandingBalance -= amount;

        if (loan.OutstandingBalance <= 0)
        {
            loan.Status         = LoanStatus.Settled;
            loan.SettlementDate = DateTime.UtcNow;
        }
        else if (loan.Status == LoanStatus.Arrears && amount >= loan.ArrearsAmount)
        {
            loan.ArrearsAmount  = 0;
            loan.MissedPayments = 0;
            loan.Status         = LoanStatus.Active;
        }

        _loanRepo.Update(loan);
        _audit.Log("LOAN_REPAYMENT", processedBy,
            $"Repayment of R{amount:F2} on loan {loanReference}", loanReference);

        return OperationResult<Loan>.Success(loan, "Repayment processed.");
    }

    /// <summary>
    /// Calculates the total amount required to settle the loan early.
    /// Settlement = Outstanding Balance + Early Settlement Fee (1.5%)
    /// </summary>
    public OperationResult<decimal> CalculateSettlementAmount(string loanReference)
    {
        var loan = _loanRepo.GetByReference(loanReference);
        if (loan == null) return OperationResult<decimal>.Failure("Loan not found.");
        if (loan.Status != LoanStatus.Active && loan.Status != LoanStatus.Arrears)
            return OperationResult<decimal>.Failure("Settlement only available for Active or Arrears loans.");

        // BUG-012: Fee is calculated on the PRINCIPAL instead of OUTSTANDING balance.
        decimal fee              = loan.PrincipalAmount * EarlySettlementFee;
        decimal settlementAmount = loan.OutstandingBalance + fee;

        return OperationResult<decimal>.Success(settlementAmount);
    }

    public OperationResult<Loan> SettleLoan(string loanReference, string processedBy)
    {
        var settlement = CalculateSettlementAmount(loanReference);
        if (!settlement.IsSuccess) return OperationResult<Loan>.Failure(settlement.Message);

        var loan = _loanRepo.GetByReference(loanReference)!;
        loan.OutstandingBalance = 0;
        loan.Status             = LoanStatus.Settled;
        loan.SettlementDate     = DateTime.UtcNow;
        _loanRepo.Update(loan);

        _audit.Log("LOAN_SETTLED", processedBy, $"Loan {loanReference} settled early.", loanReference);
        return OperationResult<Loan>.Success(loan, $"Loan settled. Total paid: R{settlement.Data:F2}");
    }

    public OperationResult<Loan> GetLoan(string loanReference)
    {
        var loan = _loanRepo.GetByReference(loanReference);
        return loan == null
            ? OperationResult<Loan>.Failure("Loan not found.")
            : OperationResult<Loan>.Success(loan);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// PMT formula: M = P * [r(1+r)^n] / [(1+r)^n - 1]
    /// where r = monthly rate, n = number of months
    /// </summary>
    private static decimal CalculatePMT(decimal principal, decimal annualRate, int months)
    {
        if (annualRate == 0) return Math.Round(principal / months, 2);
        double r = (double)annualRate / 12.0;
        double n = months;
        double pmt = (double)principal * (r * Math.Pow(1 + r, n)) / (Math.Pow(1 + r, n) - 1);
        return Math.Round((decimal)pmt, 2);
    }

    private static List<LoanRepayment> BuildRepaymentSchedule(Loan loan)
    {
        var schedule   = new List<LoanRepayment>();
        decimal balance = loan.PrincipalAmount;
        double r        = (double)loan.InterestRate / 12.0;

        for (int i = 1; i <= loan.TermMonths; i++)
        {
            decimal interest    = Math.Round(balance * (decimal)r, 2);
            decimal principal   = loan.MonthlyInstalment - interest;
            decimal closing     = balance - principal;
            if (i == loan.TermMonths) closing = 0m; // final instalment clears balance

            schedule.Add(new LoanRepayment
            {
                InstallmentNumber = i,
                DueDate           = (loan.DisbursementDate ?? DateTime.UtcNow).AddMonths(i),
                OpeningBalance    = balance,
                InstallmentAmount = loan.MonthlyInstalment,
                InterestPortion   = interest,
                PrincipalPortion  = principal,
                ClosingBalance    = closing < 0 ? 0 : closing
            });

            balance = closing < 0 ? 0 : closing;
            if (balance == 0) break;
        }
        return schedule;
    }
}

using BankCore.Core.Interfaces;
using BankCore.Core.Models;

namespace BankCore.App.Menus;

public class MainMenu
{
    private readonly IAuthService        _auth;
    private readonly IAccountService     _accounts;
    private readonly ITransactionService _transactions;
    private readonly ILoanService        _loans;
    private readonly IInterestCalculator _interest;
    private readonly IReportingService   _reports;
    private readonly IValidationService  _validator;

    private Session? _session;

    public MainMenu(IAuthService auth, IAccountService accounts,
        ITransactionService transactions, ILoanService loans,
        IInterestCalculator interest, IReportingService reports,
        IValidationService validator)
    {
        _auth         = auth;
        _accounts     = accounts;
        _transactions = transactions;
        _loans        = loans;
        _interest     = interest;
        _reports      = reports;
        _validator    = validator;
    }

    public void Run()
    {
        Console.Clear();
        ConsoleUi.Header("BankCore Enterprise Management System v1.0");
        ConsoleUi.Info("Welcome. Please log in to continue.");

        while (true)
        {
            if (_session == null)
            {
                ShowLoginScreen();
            }
            else
            {
                ShowMainMenu();
            }
        }
    }

    private void ShowLoginScreen()
    {
        ConsoleUi.Header("Login");
        string username = ConsoleUi.Prompt("Username");
        string password = ConsoleUi.PromptSecret("Password");

        var result = _auth.Login(username, password);
        if (result.IsSuccess)
        {
            _session = result.Data;
            ConsoleUi.Success($"Welcome, {_session!.Username} [{_session.Role}]");
            ConsoleUi.PressAnyKey();
        }
        else
        {
            ConsoleUi.Error(result.Message);
            ConsoleUi.PressAnyKey();
        }
    }

    private void ShowMainMenu()
    {
        Console.Clear();
        ConsoleUi.Header($"Main Menu  |  {_session!.Username} [{_session.Role}]  |  {DateTime.Now:HH:mm}");
        Console.WriteLine();
        Console.WriteLine("  [1] Account Management");
        Console.WriteLine("  [2] Transactions");
        Console.WriteLine("  [3] Loan Management");
        Console.WriteLine("  [4] Interest Calculator");
        Console.WriteLine("  [5] Reports");
        Console.WriteLine("  [6] Change Password");
        Console.WriteLine("  [0] Logout");
        Console.WriteLine();
        string choice = ConsoleUi.Prompt("Select option");

        switch (choice)
        {
            case "1": AccountMenu();      break;
            case "2": TransactionMenu();  break;
            case "3": LoanMenu();         break;
            case "4": InterestMenu();     break;
            case "5": ReportMenu();       break;
            case "6": ChangePassword();   break;
            case "0": Logout();           break;
            default: ConsoleUi.Error("Invalid option."); ConsoleUi.PressAnyKey(); break;
        }
    }

    // ── ACCOUNT MANAGEMENT ────────────────────────────────────────────────────
    private void AccountMenu()
    {
        Console.Clear();
        ConsoleUi.Header("Account Management");
        Console.WriteLine("  [1] Create Account");
        Console.WriteLine("  [2] View Account");
        Console.WriteLine("  [3] Update Account");
        Console.WriteLine("  [4] Close Account");
        Console.WriteLine("  [5] Set Dormant");
        Console.WriteLine("  [6] Reactivate Account");
        Console.WriteLine("  [0] Back");
        string choice = ConsoleUi.Prompt("Select option");

        switch (choice)
        {
            case "1": CreateAccount();     break;
            case "2": ViewAccount();       break;
            case "3": UpdateAccount();     break;
            case "4": CloseAccount();      break;
            case "5": SetDormant();        break;
            case "6": ReactivateAccount(); break;
            case "0": return;
            default: ConsoleUi.Error("Invalid option."); ConsoleUi.PressAnyKey(); break;
        }
    }

    private void CreateAccount()
    {
        if (!_auth.HasPermission(_session!.Token, "CREATE_ACCOUNT"))
        { ConsoleUi.Error("Permission denied."); ConsoleUi.PressAnyKey(); return; }

        ConsoleUi.Header("Create New Account");
        string name    = ConsoleUi.Prompt("Owner Full Name");
        string idNum   = ConsoleUi.Prompt("SA ID Number");
        string typeStr = ConsoleUi.Prompt("Account Type (Savings/Current/FixedDeposit/Notice)");
        string depStr  = ConsoleUi.Prompt("Initial Deposit (R)");
        string branch  = ConsoleUi.Prompt("Branch Code (6 digits)");

        if (!Enum.TryParse<AccountType>(typeStr, true, out var accountType))
        { ConsoleUi.Error("Invalid account type."); ConsoleUi.PressAnyKey(); return; }

        if (!decimal.TryParse(depStr, out decimal deposit))
        { ConsoleUi.Error("Invalid amount."); ConsoleUi.PressAnyKey(); return; }

        var result = _accounts.CreateAccount(name, idNum, accountType, deposit, branch);
        if (result.IsSuccess)
        {
            ConsoleUi.Success($"Account created: {result.Data!.AccountNumber}");
            ConsoleUi.Info($"Balance: R{result.Data.Balance:F2}");
        }
        else ConsoleUi.Error(result.Message);
        ConsoleUi.PressAnyKey();
    }

    private void ViewAccount()
    {
        ConsoleUi.Header("View Account");
        string accNum = ConsoleUi.Prompt("Account Number");
        var result = _accounts.GetAccountByNumber(accNum);
        if (!result.IsSuccess) { ConsoleUi.Error(result.Message); ConsoleUi.PressAnyKey(); return; }
        var acc = result.Data!;

        ConsoleUi.Separator();
        ConsoleUi.Info($"Account  : {acc.AccountNumber}");
        ConsoleUi.Info($"Owner    : {acc.OwnerName}");
        ConsoleUi.Info($"Type     : {acc.Type}");
        ConsoleUi.Info($"Status   : {acc.Status}");
        ConsoleUi.Info($"Balance  : R{acc.Balance:N2}");
        ConsoleUi.Info($"Opened   : {acc.DateOpened:yyyy-MM-dd}");
        ConsoleUi.PressAnyKey();
    }

    private void UpdateAccount()
    {
        ConsoleUi.Header("Update Account");
        if (!int.TryParse(ConsoleUi.Prompt("Account ID"), out int id))
        { ConsoleUi.Error("Invalid ID."); ConsoleUi.PressAnyKey(); return; }
        string name   = ConsoleUi.Prompt("New Owner Name");
        string branch = ConsoleUi.Prompt("New Branch Code");
        var result = _accounts.UpdateAccount(id, name, branch);
        if (result.IsSuccess) ConsoleUi.Success("Account updated."); else ConsoleUi.Error(result.Message);
        ConsoleUi.PressAnyKey();
    }

    private void CloseAccount()
    {
        if (!_auth.HasPermission(_session!.Token, "CLOSE_ACCOUNT"))
        { ConsoleUi.Error("Permission denied."); ConsoleUi.PressAnyKey(); return; }

        ConsoleUi.Header("Close Account");
        if (!int.TryParse(ConsoleUi.Prompt("Account ID"), out int id))
        { ConsoleUi.Error("Invalid ID."); ConsoleUi.PressAnyKey(); return; }
        var result = _accounts.CloseAccount(id, _session!.Username);
        if (result.IsSuccess) ConsoleUi.Success("Account closed."); else ConsoleUi.Error(result.Message);
        ConsoleUi.PressAnyKey();
    }

    private void SetDormant()
    {
        ConsoleUi.Header("Set Account Dormant");
        if (!int.TryParse(ConsoleUi.Prompt("Account ID"), out int id))
        { ConsoleUi.Error("Invalid ID."); ConsoleUi.PressAnyKey(); return; }
        var result = _accounts.SetDormant(id);
        if (result.IsSuccess) ConsoleUi.Success("Account set to dormant."); else ConsoleUi.Error(result.Message);
        ConsoleUi.PressAnyKey();
    }

    private void ReactivateAccount()
    {
        ConsoleUi.Header("Reactivate Account");
        if (!int.TryParse(ConsoleUi.Prompt("Account ID"), out int id))
        { ConsoleUi.Error("Invalid ID."); ConsoleUi.PressAnyKey(); return; }
        if (!decimal.TryParse(ConsoleUi.Prompt("Reactivation Deposit (R)"), out decimal dep))
        { ConsoleUi.Error("Invalid amount."); ConsoleUi.PressAnyKey(); return; }
        var result = _accounts.ReactivateAccount(id, dep);
        if (result.IsSuccess) ConsoleUi.Success("Account reactivated."); else ConsoleUi.Error(result.Message);
        ConsoleUi.PressAnyKey();
    }

    // ── TRANSACTIONS ───────────────────────────────────────────────────────────
    private void TransactionMenu()
    {
        Console.Clear();
        ConsoleUi.Header("Transaction Menu");
        Console.WriteLine("  [1] Deposit");
        Console.WriteLine("  [2] Withdraw");
        Console.WriteLine("  [3] Transfer");
        Console.WriteLine("  [4] Reverse Transaction");
        Console.WriteLine("  [5] Transaction History");
        Console.WriteLine("  [0] Back");
        string choice = ConsoleUi.Prompt("Select option");
        switch (choice)
        {
            case "1": DoDeposit();  break;
            case "2": DoWithdraw(); break;
            case "3": DoTransfer(); break;
            case "4": DoReversal(); break;
            case "5": DoHistory();  break;
            case "0": return;
            default: ConsoleUi.Error("Invalid option."); ConsoleUi.PressAnyKey(); break;
        }
    }

    private void DoDeposit()
    {
        if (!_auth.HasPermission(_session!.Token, "DEPOSIT"))
        { ConsoleUi.Error("Permission denied."); ConsoleUi.PressAnyKey(); return; }

        ConsoleUi.Header("Deposit");
        if (!int.TryParse(ConsoleUi.Prompt("Account ID"), out int id)) { ConsoleUi.Error("Invalid ID."); ConsoleUi.PressAnyKey(); return; }
        if (!decimal.TryParse(ConsoleUi.Prompt("Amount (R)"), out decimal amt)) { ConsoleUi.Error("Invalid amount."); ConsoleUi.PressAnyKey(); return; }
        string desc = ConsoleUi.Prompt("Description");
        var result = _transactions.Deposit(id, amt, desc, _session!.Username);
        if (result.IsSuccess) { ConsoleUi.Success($"Deposit successful. Ref: {result.Data!.TransactionReference}"); ConsoleUi.Info($"New Balance: R{result.Data.BalanceAfter:N2}"); }
        else ConsoleUi.Error(result.Message);
        ConsoleUi.PressAnyKey();
    }

    private void DoWithdraw()
    {
        if (!_auth.HasPermission(_session!.Token, "WITHDRAW"))
        { ConsoleUi.Error("Permission denied."); ConsoleUi.PressAnyKey(); return; }

        ConsoleUi.Header("Withdrawal");
        if (!int.TryParse(ConsoleUi.Prompt("Account ID"), out int id)) { ConsoleUi.Error("Invalid ID."); ConsoleUi.PressAnyKey(); return; }
        if (!decimal.TryParse(ConsoleUi.Prompt("Amount (R)"), out decimal amt)) { ConsoleUi.Error("Invalid amount."); ConsoleUi.PressAnyKey(); return; }
        string desc = ConsoleUi.Prompt("Description");
        var result = _transactions.Withdraw(id, amt, desc, _session!.Username);
        if (result.IsSuccess) { ConsoleUi.Success($"Withdrawal successful. Ref: {result.Data!.TransactionReference}"); ConsoleUi.Info($"New Balance: R{result.Data.BalanceAfter:N2}"); }
        else ConsoleUi.Error(result.Message);
        ConsoleUi.PressAnyKey();
    }

    private void DoTransfer()
    {
        if (!_auth.HasPermission(_session!.Token, "TRANSFER"))
        { ConsoleUi.Error("Permission denied."); ConsoleUi.PressAnyKey(); return; }

        ConsoleUi.Header("Transfer");
        if (!int.TryParse(ConsoleUi.Prompt("From Account ID"), out int from)) { ConsoleUi.Error("Invalid ID."); ConsoleUi.PressAnyKey(); return; }
        if (!int.TryParse(ConsoleUi.Prompt("To Account ID"), out int to)) { ConsoleUi.Error("Invalid ID."); ConsoleUi.PressAnyKey(); return; }
        if (!decimal.TryParse(ConsoleUi.Prompt("Amount (R)"), out decimal amt)) { ConsoleUi.Error("Invalid amount."); ConsoleUi.PressAnyKey(); return; }
        string desc = ConsoleUi.Prompt("Description");
        var result = _transactions.Transfer(from, to, amt, desc, _session!.Username);
        if (result.IsSuccess) ConsoleUi.Success($"Transfer successful. Ref: {result.Data!.TransactionReference}");
        else ConsoleUi.Error(result.Message);
        ConsoleUi.PressAnyKey();
    }

    private void DoReversal()
    {
        if (!_auth.HasPermission(_session!.Token, "REVERSE_TRANSACTION"))
        { ConsoleUi.Error("Permission denied."); ConsoleUi.PressAnyKey(); return; }

        ConsoleUi.Header("Reverse Transaction");
        string txnRef = ConsoleUi.Prompt("Transaction Reference");
        string reason = ConsoleUi.Prompt("Reason");
        var result = _transactions.ReverseTransaction(txnRef, reason, _session!.Username);
        if (result.IsSuccess) ConsoleUi.Success($"Reversed. New Ref: {result.Data!.TransactionReference}");
        else ConsoleUi.Error(result.Message);
        ConsoleUi.PressAnyKey();
    }

    private void DoHistory()
    {
        ConsoleUi.Header("Transaction History");
        if (!int.TryParse(ConsoleUi.Prompt("Account ID"), out int id)) { ConsoleUi.Error("Invalid ID."); ConsoleUi.PressAnyKey(); return; }
        var result = _transactions.GetTransactionHistory(id);
        if (!result.IsSuccess) { ConsoleUi.Error(result.Message); ConsoleUi.PressAnyKey(); return; }
        var txns = result.Data!;
        if (!txns.Any()) { ConsoleUi.Info("No transactions found."); ConsoleUi.PressAnyKey(); return; }
        var rows = txns.Select(t => new[] { t.TransactionReference[..20], t.Type.ToString(), $"R{t.Amount:N2}", t.Status.ToString(), t.Timestamp.ToString("yyyy-MM-dd HH:mm") }).ToList();
        ConsoleUi.Table(new[] { "Reference", "Type", "Amount", "Status", "Date" }, rows, new[] { 20, 12, 12, 10, 16 });
        ConsoleUi.PressAnyKey();
    }

    // ── LOAN MANAGEMENT ────────────────────────────────────────────────────────
    private void LoanMenu()
    {
        Console.Clear();
        ConsoleUi.Header("Loan Management");
        Console.WriteLine("  [1] Apply for Loan");
        Console.WriteLine("  [2] Approve Loan");
        Console.WriteLine("  [3] Reject Loan");
        Console.WriteLine("  [4] View Repayment Schedule");
        Console.WriteLine("  [5] Process Repayment");
        Console.WriteLine("  [6] Calculate Settlement");
        Console.WriteLine("  [0] Back");
        string c = ConsoleUi.Prompt("Select option");
        switch (c)
        {
            case "1": ApplyLoan();          break;
            case "2": ApproveLoan();        break;
            case "3": RejectLoan();         break;
            case "4": ViewRepayments();     break;
            case "5": ProcessRepayment();   break;
            case "6": CalcSettlement();     break;
            case "0": return;
            default: ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); break;
        }
    }

    private void ApplyLoan()
    {
        ConsoleUi.Header("Loan Application");
        if (!int.TryParse(ConsoleUi.Prompt("Account ID"), out int accId)) { ConsoleUi.Error("Invalid ID."); ConsoleUi.PressAnyKey(); return; }
        string typeStr = ConsoleUi.Prompt("Loan Type (Personal/Home/Vehicle/Business)");
        if (!Enum.TryParse<LoanType>(typeStr, true, out var lt)) { ConsoleUi.Error("Invalid type."); ConsoleUi.PressAnyKey(); return; }
        if (!decimal.TryParse(ConsoleUi.Prompt("Amount (R)"), out decimal amt)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        if (!int.TryParse(ConsoleUi.Prompt("Term (months)"), out int term)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        if (!decimal.TryParse(ConsoleUi.Prompt("Annual Rate (e.g. 0.12 for 12%)"), out decimal rate)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        if (!decimal.TryParse(ConsoleUi.Prompt("Monthly Income (R)"), out decimal income)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        if (!decimal.TryParse(ConsoleUi.Prompt("Existing Monthly Debt (R)"), out decimal debt)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        if (!int.TryParse(ConsoleUi.Prompt("Credit Score"), out int score)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }

        var result = _loans.ApplyForLoan(accId, lt, amt, term, rate, income, debt, score);
        if (result.IsSuccess) { ConsoleUi.Success($"Application submitted. Ref: {result.Data!.LoanReference}"); ConsoleUi.Info($"Monthly Instalment: R{result.Data.MonthlyInstalment:N2}"); }
        else ConsoleUi.Error(result.Message);
        ConsoleUi.PressAnyKey();
    }

    private void ApproveLoan()
    {
        if (!_auth.HasPermission(_session!.Token, "APPROVE_LOAN")) { ConsoleUi.Error("Permission denied."); ConsoleUi.PressAnyKey(); return; }
        ConsoleUi.Header("Approve Loan");
        string lref = ConsoleUi.Prompt("Loan Reference");
        var result = _loans.ApproveLoan(lref, _session!.Username);
        if (result.IsSuccess) ConsoleUi.Success("Loan approved and disbursed."); else ConsoleUi.Error(result.Message);
        ConsoleUi.PressAnyKey();
    }

    private void RejectLoan()
    {
        if (!_auth.HasPermission(_session!.Token, "REJECT_LOAN")) { ConsoleUi.Error("Permission denied."); ConsoleUi.PressAnyKey(); return; }
        ConsoleUi.Header("Reject Loan");
        string lref   = ConsoleUi.Prompt("Loan Reference");
        string reason = ConsoleUi.Prompt("Reason");
        var result = _loans.RejectLoan(lref, reason, _session!.Username);
        if (result.IsSuccess) ConsoleUi.Success("Loan rejected."); else ConsoleUi.Error(result.Message);
        ConsoleUi.PressAnyKey();
    }

    private void ViewRepayments()
    {
        ConsoleUi.Header("Repayment Schedule");
        string lref = ConsoleUi.Prompt("Loan Reference");
        var result = _loans.GenerateRepaymentSchedule(lref);
        if (!result.IsSuccess) { ConsoleUi.Error(result.Message); ConsoleUi.PressAnyKey(); return; }
        var rows = result.Data!.Take(6).Select(r => new[]
        {
            r.InstallmentNumber.ToString(),
            r.DueDate.ToString("yyyy-MM-dd"),
            $"R{r.OpeningBalance:N2}",
            $"R{r.PrincipalPortion:N2}",
            $"R{r.InterestPortion:N2}",
            $"R{r.ClosingBalance:N2}"
        }).ToList();
        ConsoleUi.Table(new[] { "#", "Due Date", "Opening", "Principal", "Interest", "Closing" }, rows, new[] { 3, 12, 12, 12, 12, 12 });
        ConsoleUi.Info($"(Showing first 6 of {result.Data.Count} instalments)");
        ConsoleUi.PressAnyKey();
    }

    private void ProcessRepayment()
    {
        ConsoleUi.Header("Process Loan Repayment");
        string lref = ConsoleUi.Prompt("Loan Reference");
        if (!decimal.TryParse(ConsoleUi.Prompt("Repayment Amount (R)"), out decimal amt)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        var result = _loans.ProcessRepayment(lref, amt, _session!.Username);
        if (result.IsSuccess) { ConsoleUi.Success("Repayment processed."); ConsoleUi.Info($"Outstanding Balance: R{result.Data!.OutstandingBalance:N2}"); ConsoleUi.Info($"Loan Status: {result.Data.Status}"); }
        else ConsoleUi.Error(result.Message);
        ConsoleUi.PressAnyKey();
    }

    private void CalcSettlement()
    {
        ConsoleUi.Header("Calculate Settlement Amount");
        string lref = ConsoleUi.Prompt("Loan Reference");
        var result = _loans.CalculateSettlementAmount(lref);
        if (result.IsSuccess) ConsoleUi.Info($"Settlement Amount: R{result.Data:N2} (includes 1.5% early settlement fee)");
        else ConsoleUi.Error(result.Message);
        ConsoleUi.PressAnyKey();
    }

    // ── INTEREST CALCULATOR ────────────────────────────────────────────────────
    private void InterestMenu()
    {
        Console.Clear();
        ConsoleUi.Header("Interest Calculator");
        Console.WriteLine("  [1] Simple Interest");
        Console.WriteLine("  [2] Compound Interest");
        Console.WriteLine("  [3] Daily Interest");
        Console.WriteLine("  [4] Future Value");
        Console.WriteLine("  [0] Back");
        string c = ConsoleUi.Prompt("Select option");
        switch (c)
        {
            case "1": CalcSimple();   break;
            case "2": CalcCompound(); break;
            case "3": CalcDaily();    break;
            case "4": CalcFV();       break;
            case "0": return;
        }
    }

    private void CalcSimple()
    {
        ConsoleUi.Header("Simple Interest");
        if (!decimal.TryParse(ConsoleUi.Prompt("Principal (R)"), out decimal p)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        if (!decimal.TryParse(ConsoleUi.Prompt("Annual Rate (e.g. 0.085)"), out decimal r)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        if (!int.TryParse(ConsoleUi.Prompt("Months"), out int m)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        decimal interest = _interest.SimpleInterest(p, r, m);
        ConsoleUi.Info($"Interest: R{interest:N2}");
        ConsoleUi.Info($"Total:    R{p + interest:N2}");
        ConsoleUi.PressAnyKey();
    }

    private void CalcCompound()
    {
        ConsoleUi.Header("Compound Interest");
        if (!decimal.TryParse(ConsoleUi.Prompt("Principal (R)"), out decimal p)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        if (!decimal.TryParse(ConsoleUi.Prompt("Annual Rate (e.g. 0.085)"), out decimal r)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        if (!int.TryParse(ConsoleUi.Prompt("Months"), out int m)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        if (!int.TryParse(ConsoleUi.Prompt("Compounding Frequency (1/2/4/12/365)"), out int freq)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        decimal interest = _interest.CompoundInterest(p, r, m, freq);
        ConsoleUi.Info($"Interest: R{interest:N2}");
        ConsoleUi.Info($"Total:    R{p + interest:N2}");
        ConsoleUi.PressAnyKey();
    }

    private void CalcDaily()
    {
        ConsoleUi.Header("Daily Interest Accrual");
        if (!decimal.TryParse(ConsoleUi.Prompt("Principal (R)"), out decimal p)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        if (!decimal.TryParse(ConsoleUi.Prompt("Annual Rate (e.g. 0.085)"), out decimal r)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        if (!int.TryParse(ConsoleUi.Prompt("Number of Days"), out int d)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        decimal interest = _interest.DailyInterest(p, r, d);
        ConsoleUi.Info($"Daily Interest ({d} days): R{interest:N2}");
        ConsoleUi.PressAnyKey();
    }

    private void CalcFV()
    {
        ConsoleUi.Header("Future Value");
        if (!decimal.TryParse(ConsoleUi.Prompt("Principal (R)"), out decimal p)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        if (!decimal.TryParse(ConsoleUi.Prompt("Annual Rate (e.g. 0.085)"), out decimal r)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        if (!int.TryParse(ConsoleUi.Prompt("Months"), out int m)) { ConsoleUi.Error("Invalid."); ConsoleUi.PressAnyKey(); return; }
        string compStr = ConsoleUi.Prompt("Compound interest? (y/n)");
        bool isCompound = compStr.Equals("y", StringComparison.OrdinalIgnoreCase);
        decimal fv = _interest.FutureValue(p, r, m, isCompound);
        ConsoleUi.Info($"Future Value: R{fv:N2}");
        ConsoleUi.PressAnyKey();
    }

    // ── REPORTS ────────────────────────────────────────────────────────────────
    private void ReportMenu()
    {
        if (!_auth.HasPermission(_session!.Token, "GENERATE_REPORT"))
        { ConsoleUi.Error("Permission denied."); ConsoleUi.PressAnyKey(); return; }

        Console.Clear();
        ConsoleUi.Header("Reports");
        Console.WriteLine("  [1] Account Statement");
        Console.WriteLine("  [2] System Summary");
        Console.WriteLine("  [3] Audit Log");
        Console.WriteLine("  [0] Back");
        string c = ConsoleUi.Prompt("Select option");
        switch (c)
        {
            case "1": Statement();    break;
            case "2": Summary();      break;
            case "3": AuditLog();     break;
            case "0": return;
        }
    }

    private void Statement()
    {
        ConsoleUi.Header("Account Statement");
        if (!int.TryParse(ConsoleUi.Prompt("Account ID"), out int id)) { ConsoleUi.Error("Invalid ID."); ConsoleUi.PressAnyKey(); return; }
        if (!DateTime.TryParse(ConsoleUi.Prompt("From Date (yyyy-MM-dd)"), out DateTime from)) { ConsoleUi.Error("Invalid date."); ConsoleUi.PressAnyKey(); return; }
        if (!DateTime.TryParse(ConsoleUi.Prompt("To Date (yyyy-MM-dd)"), out DateTime to)) { ConsoleUi.Error("Invalid date."); ConsoleUi.PressAnyKey(); return; }
        var result = _reports.GenerateStatement(id, from, to);
        if (!result.IsSuccess) { ConsoleUi.Error(result.Message); ConsoleUi.PressAnyKey(); return; }
        var s = result.Data!;
        ConsoleUi.Separator();
        ConsoleUi.Info($"Account : {s.Account.AccountNumber}  |  {s.Account.OwnerName}");
        ConsoleUi.Info($"Period  : {s.FromDate:yyyy-MM-dd} to {s.ToDate:yyyy-MM-dd}");
        ConsoleUi.Info($"Opening : R{s.OpeningBalance:N2}   Credits: R{s.TotalCredits:N2}   Debits: R{s.TotalDebits:N2}   Closing: R{s.ClosingBalance:N2}");
        ConsoleUi.PressAnyKey();
    }

    private void Summary()
    {
        var result = _reports.GenerateSummaryReport(DateTime.Today);
        Console.WriteLine(result.Data);
        ConsoleUi.PressAnyKey();
    }

    private void AuditLog()
    {
        ConsoleUi.Header("Audit Log");
        if (!DateTime.TryParse(ConsoleUi.Prompt("From Date"), out DateTime from)) from = DateTime.UtcNow.AddDays(-7);
        if (!DateTime.TryParse(ConsoleUi.Prompt("To Date"), out DateTime to)) to = DateTime.UtcNow;
        var result = _reports.GetAuditLog(from, to);
        if (!result.IsSuccess) { ConsoleUi.Error(result.Message); ConsoleUi.PressAnyKey(); return; }
        var rows = result.Data!.Take(20).Select(l => new[] { l.Timestamp.ToString("MM-dd HH:mm"), l.EventType[..Math.Min(18, l.EventType.Length)], l.Username, l.IsSuccessful ? "OK" : "FAIL" }).ToList();
        ConsoleUi.Table(new[] { "Time", "Event", "User", "Result" }, rows, new[] { 12, 18, 12, 6 });
        ConsoleUi.PressAnyKey();
    }

    // ── PASSWORD CHANGE ────────────────────────────────────────────────────────
    private void ChangePassword()
    {
        ConsoleUi.Header("Change Password");
        string current = ConsoleUi.PromptSecret("Current Password");
        string newPwd  = ConsoleUi.PromptSecret("New Password");
        string confirm = ConsoleUi.PromptSecret("Confirm New Password");
        if (newPwd != confirm) { ConsoleUi.Error("Passwords do not match."); ConsoleUi.PressAnyKey(); return; }
        var result = _auth.ChangePassword(_session!.Token, current, newPwd);
        if (result.IsSuccess) ConsoleUi.Success("Password changed successfully."); else ConsoleUi.Error(result.Message);
        ConsoleUi.PressAnyKey();
    }

    // ── LOGOUT ─────────────────────────────────────────────────────────────────
    private void Logout()
    {
        _auth.Logout(_session!.Token);
        _session = null;
        ConsoleUi.Info("You have been logged out.");
        ConsoleUi.PressAnyKey();
    }
}

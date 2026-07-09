using BankCore.Core.Services;
using BankCore.Data.Repositories;
using BankCore.Data.Seed;
using BankCore.App.Menus;

// ── Compose the application ──────────────────────────────────────────────────
var accountRepo  = new InMemoryAccountRepository();
var txnRepo      = new InMemoryTransactionRepository();
var loanRepo     = new InMemoryLoanRepository();
var userRepo     = new InMemoryUserRepository();
var auditRepo    = new InMemoryAuditRepository();
var sessionRepo  = new InMemorySessionRepository();

var hasher      = new PasswordHasher();
var validator   = new ValidationService();
var auditSvc    = new AuditService(auditRepo);

var accountSvc  = new AccountService(accountRepo, validator, auditSvc);
var txnSvc      = new TransactionService(accountRepo, txnRepo, validator, auditSvc);
var interestCalc = new InterestCalculator();
var loanSvc     = new LoanService(loanRepo, accountRepo, auditSvc);
var authSvc     = new AuthService(userRepo, sessionRepo, hasher, auditSvc, validator);
var reportSvc   = new ReportingService(accountRepo, txnRepo, auditRepo);

// ── Seed demo data ────────────────────────────────────────────────────────────
DataSeeder.Seed(accountRepo, userRepo, hasher);

// ── Launch ────────────────────────────────────────────────────────────────────
Console.Title = "BankCore Enterprise Management System v1.0";
var shell = new MainMenu(authSvc, accountSvc, txnSvc, loanSvc, interestCalc, reportSvc, validator);
shell.Run();

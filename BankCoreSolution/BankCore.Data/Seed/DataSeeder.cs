using BankCore.Core.Interfaces;
using BankCore.Core.Models;
using BankCore.Core.Services;

namespace BankCore.Data.Seed;

/// <summary>
/// Seeds the in-memory store with realistic demo data for simulation use.
/// </summary>
public static class DataSeeder
{
    public static void Seed(
        IAccountRepository accountRepo,
        IUserRepository userRepo,
        IPasswordHasher hasher)
    {
        SeedUsers(userRepo, hasher);
        SeedAccounts(accountRepo);
    }

    private static void SeedUsers(IUserRepository repo, IPasswordHasher hasher)
    {
        var users = new[]
        {
            ("admin",   "Admin@1234!",   UserRole.Admin),
            ("manager", "Manager@1234!", UserRole.Manager),
            ("teller1", "Teller@1234!",  UserRole.Teller),
            ("auditor", "Audit@1234!",   UserRole.Auditor),
        };

        foreach (var (uname, pwd, role) in users)
        {
            if (!repo.UsernameExists(uname))
            {
                var (hash, salt) = hasher.HashPassword(pwd);
                repo.Add(new User
                {
                    Username     = uname,
                    PasswordHash = hash,
                    Salt         = salt,
                    Role         = role,
                    IsLocked     = false
                });
            }
        }
    }

    private static void SeedAccounts(IAccountRepository repo)
    {
        var accounts = new List<Account>
        {
            new Account
            {
                AccountNumber        = "BC1000000001",
                OwnerName            = "Thabo Nkosi",
                OwnerIdNumber        = "8501015009087",
                Type                 = AccountType.Savings,
                Status               = AccountStatus.Active,
                Balance              = 12500.00m,
                DailyWithdrawalLimit = 5000m,
                DateOpened           = DateTime.UtcNow.AddYears(-2),
                LastActivityDate     = DateTime.UtcNow.AddDays(-1),
                BranchCode           = "632005"
            },
            new Account
            {
                AccountNumber        = "BC1000000002",
                OwnerName            = "Priya Pillay",
                OwnerIdNumber        = "9203024567089",
                Type                 = AccountType.Current,
                Status               = AccountStatus.Active,
                Balance              = 48000.00m,
                DailyWithdrawalLimit = 20000m,
                DateOpened           = DateTime.UtcNow.AddYears(-1),
                LastActivityDate     = DateTime.UtcNow,
                BranchCode           = "051001"
            },
            new Account
            {
                AccountNumber        = "BC1000000003",
                OwnerName            = "Jacques van der Merwe",
                OwnerIdNumber        = "7712035012083",
                Type                 = AccountType.FixedDeposit,
                Status               = AccountStatus.Active,
                Balance              = 150000.00m,
                DailyWithdrawalLimit = 0m,
                DateOpened           = DateTime.UtcNow.AddMonths(-6),
                LastActivityDate     = DateTime.UtcNow.AddMonths(-6),
                BranchCode           = "632005"
            },
            new Account
            {
                AccountNumber        = "BC1000000004",
                OwnerName            = "Nomsa Dlamini",
                OwnerIdNumber        = "8804106123086",
                Type                 = AccountType.Savings,
                Status               = AccountStatus.Dormant,
                Balance              = 250.00m,
                DailyWithdrawalLimit = 2000m,
                DateOpened           = DateTime.UtcNow.AddYears(-3),
                LastActivityDate     = DateTime.UtcNow.AddMonths(-13),
                BranchCode           = "210554"
            },
            new Account
            {
                AccountNumber        = "BC1000000005",
                OwnerName            = "David Chen",
                OwnerIdNumber        = "9505155678081",
                Type                 = AccountType.Current,
                Status               = AccountStatus.Closed,
                Balance              = 0m,
                DailyWithdrawalLimit = 10000m,
                DateOpened           = DateTime.UtcNow.AddYears(-4),
                DateClosed           = DateTime.UtcNow.AddMonths(-2),
                LastActivityDate     = DateTime.UtcNow.AddMonths(-2),
                BranchCode           = "051001"
            }
        };

        foreach (var acc in accounts)
            repo.Add(acc);
    }
}

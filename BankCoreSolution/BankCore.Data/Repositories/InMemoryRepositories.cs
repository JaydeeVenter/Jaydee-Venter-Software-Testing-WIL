using BankCore.Core.Interfaces;
using BankCore.Core.Models;

namespace BankCore.Data.Repositories;

public class InMemoryAccountRepository : IAccountRepository
{
    private readonly List<Account> _store = new();
    private int _nextId = 1;

    public Account? GetById(int id) => _store.FirstOrDefault(a => a.Id == id);
    public Account? GetByAccountNumber(string n) => _store.FirstOrDefault(a => a.AccountNumber == n);
    public List<Account> GetAll() => _store.ToList();
    public List<Account> GetByOwnerIdNumber(string id) =>
        _store.Where(a => a.OwnerIdNumber == id).ToList();

    public void Add(Account account)
    {
        account.Id = _nextId++;
        _store.Add(account);
    }

    public void Update(Account account)
    {
        var idx = _store.FindIndex(a => a.Id == account.Id);
        if (idx >= 0) _store[idx] = account;
    }

    public void Delete(int id) => _store.RemoveAll(a => a.Id == id);
    public bool AccountNumberExists(string n) => _store.Any(a => a.AccountNumber == n);
}

public class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly List<Transaction> _store = new();
    private int _nextId = 1;

    public Transaction? GetById(int id) => _store.FirstOrDefault(t => t.Id == id);
    public Transaction? GetByReference(string r) => _store.FirstOrDefault(t => t.TransactionReference == r);
    public List<Transaction> GetByAccountId(int id) =>
        _store.Where(t => t.AccountId == id || t.TargetAccountId == id).ToList();

    public List<Transaction> GetByDateRange(int accountId, DateTime from, DateTime to) =>
        _store.Where(t => (t.AccountId == accountId || t.TargetAccountId == accountId)
                       && t.Timestamp >= from && t.Timestamp <= to).ToList();

    public void Add(Transaction t) { t.Id = _nextId++; _store.Add(t); }
    public void Update(Transaction t)
    {
        var idx = _store.FindIndex(x => x.Id == t.Id);
        if (idx >= 0) _store[idx] = t;
    }
    public bool ReferenceExists(string r) => _store.Any(t => t.TransactionReference == r);
}

public class InMemoryLoanRepository : ILoanRepository
{
    private readonly List<Loan> _store = new();
    private int _nextId = 1;

    public Loan? GetById(int id) => _store.FirstOrDefault(l => l.Id == id);
    public Loan? GetByReference(string r) => _store.FirstOrDefault(l => l.LoanReference == r);
    public List<Loan> GetByAccountId(int id) => _store.Where(l => l.AccountId == id).ToList();
    public List<Loan> GetByStatus(LoanStatus s) => _store.Where(l => l.Status == s).ToList();
    public void Add(Loan l) { l.Id = _nextId++; _store.Add(l); }
    public void Update(Loan l)
    {
        var idx = _store.FindIndex(x => x.Id == l.Id);
        if (idx >= 0) _store[idx] = l;
    }
}

public class InMemoryUserRepository : IUserRepository
{
    private readonly List<User> _store = new();
    private int _nextId = 1;

    public User? GetById(int id) => _store.FirstOrDefault(u => u.Id == id);
    public User? GetByUsername(string name) =>
        _store.FirstOrDefault(u => u.Username.Equals(name, StringComparison.OrdinalIgnoreCase));
    public List<User> GetAll() => _store.ToList();
    public void Add(User u) { u.Id = _nextId++; _store.Add(u); }
    public void Update(User u)
    {
        var idx = _store.FindIndex(x => x.Id == u.Id);
        if (idx >= 0) _store[idx] = u;
    }
    public bool UsernameExists(string name) =>
        _store.Any(u => u.Username.Equals(name, StringComparison.OrdinalIgnoreCase));
}

public class InMemoryAuditRepository : IAuditRepository
{
    private readonly List<AuditLog> _store = new();
    private int _nextId = 1;

    public void Add(AuditLog l) { l.Id = _nextId++; _store.Add(l); }
    public List<AuditLog> GetAll() => _store.ToList();
    public List<AuditLog> GetByUsername(string u) =>
        _store.Where(l => l.Username == u).ToList();
    public List<AuditLog> GetByDateRange(DateTime from, DateTime to) =>
        _store.Where(l => l.Timestamp >= from && l.Timestamp <= to).ToList();
}

public class InMemorySessionRepository : ISessionRepository
{
    private readonly List<Session> _store = new();

    public void Add(Session s) => _store.Add(s);
    public Session? GetByToken(string token) =>
        _store.FirstOrDefault(s => s.Token == token);
    public void Update(Session s)
    {
        var idx = _store.FindIndex(x => x.Token == s.Token);
        if (idx >= 0) _store[idx] = s;
    }
    public void InvalidateAllForUser(int userId)
    {
        foreach (var s in _store.Where(x => x.UserId == userId))
            s.IsActive = false;
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Moq;
using NUnit.Compatibility;
using BankCore.Core.Interfaces;
using BankCore.Core.Models;
using BankCore.Core.Services;
using System.Globalization;
using System.Security.Cryptography;

namespace BankCore.Tests.NUnit
{
    [TestFixture]
    [Category("Reporting Service")]
    public class ReportingEngineNUnitTests
    {
        private Mock<IAccountRepository> _accountRepositoryMock = null!;
        private Mock<ITransactionRepository> _transactionRepositoryMock = null!;
        private Mock<IAuditRepository> _auditRepositoryMock = null!;
        private ReportingService _reportingService = null!;

        [SetUp]
        public void Setup()
        {
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _transactionRepositoryMock = new Mock<ITransactionRepository>();
            _auditRepositoryMock = new Mock<IAuditRepository>();

            _reportingService = new ReportingService(
                _accountRepositoryMock.Object,
                _transactionRepositoryMock.Object,
                _auditRepositoryMock.Object
            );
        }

        [TearDown]
        public void TearDown()
        {
            _accountRepositoryMock = null!;
            _transactionRepositoryMock = null!;
            _auditRepositoryMock = null!;
            _reportingService = null!;
        }

        //Generate Statement Tests

        [Test]
        [Category("Positive")]
        public void GeneratingAStatement_WithValidInfo()
        {
            //arrange
            int accoountID = 1;
            DateTime from = new DateTime(2026, 5, 25);
            DateTime to = new DateTime(2026, 6, 25);

            var account = new Account
            {
                Id = accoountID,
                AccountNumber = "BC1000000001",
                Balance = 15000m,
                Status = AccountStatus.Active
            };

            var transactionsOnAccount = new List<Transaction>
            {
                new Transaction
                {
                    Type = TransactionType.Deposit,
                    Amount = 6000m,
                    BalanceBefore = 10000m,
                    Timestamp = new DateTime(2026, 5, 26)
                },
                new Transaction
                {
                    Type = TransactionType.Withdrawal,
                    Amount = 1000m,
                    BalanceBefore = 16000m,
                    Timestamp = new DateTime(2026, 6, 1)
                }
            };

            _accountRepositoryMock.Setup(repo => repo.GetById(accoountID)).Returns(account);

            _transactionRepositoryMock.Setup(repo => repo.GetByDateRange(accoountID, It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(transactionsOnAccount);

            //act
            var result = _reportingService.GenerateStatement(accoountID, from, to);

            //assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Data, Is.Not.Null);
                Assert.That(result.Data!.TotalCredits, Is.EqualTo(6000m));
                Assert.That(result.Data.TotalDebits, Is.EqualTo(1000m));
                Assert.That(result.Data.OpeningBalance, Is.EqualTo(10000m));
                Assert.That(result.Data.ClosingBalance, Is.EqualTo(15000m));
                Assert.That(result.Data.Transactions, Has.Count.EqualTo(2));
            });
        }

        [Test]
        [Category("Negative")]
        public void UsingInavlidAccount_WhenGeneratingAStatement()
        {
            //arrange
            int accountID = 6220;
            DateTime from = new DateTime(2026, 6, 15);
            DateTime to = new DateTime(2026, 6, 25);

            _accountRepositoryMock.Setup(repo => repo.GetById(accountID)).Returns((Account)null!);

            //act
            var result = _reportingService.GenerateStatement(accountID, from, to);

            //assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Message, Is.EqualTo("Account not found."));
            });

            _transactionRepositoryMock.Verify(repo => repo.GetByDateRange(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Test]
        [Category("Negative")]
        public void UsingPendingAccount_ToGenerateStatement()
        {
            //arrange
            int accountID = 2;
            DateTime from = new DateTime(2026, 6, 01);
            DateTime to = new DateTime(2026, 6, 30);

            var fakeAccount = new Account
            {
                Id = accountID,
                Status = AccountStatus.Pending,
                AccountNumber = "BC1000000002",
                Balance = 500m,
            };

            _accountRepositoryMock.Setup(repo => repo.GetById(accountID)).Returns(fakeAccount);

            //act
            var result = _reportingService.GenerateStatement(accountID, from, to);

            //assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Message, Is.EqualTo("Account is not yet active."));
            });
        }

        [TestCase("2026-07-04", "2026-06-30")]
        [TestCase("2026-06-24", "2026-06-14")]
        [Category("Negative")]
        public void AttemptingToGenerateStatement_WithFromDate_AfterToDate(string fromDateString, string toDateString)
        {
            //arrange
            var accountID = 1;
            DateTime from = DateTime.Parse(fromDateString);
            DateTime to = DateTime.Parse(toDateString);

            var account = new Account
            {
                Id = accountID,
                Status = AccountStatus.Active
            };

            _accountRepositoryMock.Setup(repo => repo.GetById(accountID)).Returns(account);

            //act
            var result = _reportingService.GenerateStatement(accountID, from, to);

            //assert

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Message, Is.EqualTo("From date cannot be later than To date."));
            });
        }

        //Audit Log Tests

        [TestCase("2026-08-08", "2026-04-15")]
        [TestCase("2026-07-20", "2026-07-14")]
        [Category("Negative")]
        public void GetAuditLog_InvalidDateRange_ReturnsError(string fromDateString, string toDateString)
        {
            //arrange
            DateTime from = DateTime.Parse(fromDateString);
            DateTime to = DateTime.Parse(toDateString);

            _auditRepositoryMock.Setup(repo => repo.GetByDateRange(from, to));

            //act
            var result = _reportingService.GetAuditLog(from, to);

            //assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Message, Is.EqualTo("Invalid date range."));
            });
        }

        [TestCase("2026-06-08", "2026-07-08")]
        [TestCase("2026-05-20", "2026-06-14")]
        [Category("Positive")]
        public void GetAuditLog_ValidDateRange(string fromDateString, string toDateString)
        {
            //arrange
            DateTime from = DateTime.Parse(fromDateString);
            DateTime to = DateTime.Parse(toDateString);

            var testAuditLogs = new List<AuditLog>
            {
                new AuditLog
                {
                    EventType = "TRANSFER", Username = "Jaydee Venter"
                },
                new AuditLog
                {
                    EventType = "DEPOSIT", Username = "Jaydee Venter"
                }
            };

            _auditRepositoryMock.Setup(repo => repo.GetByDateRange(from, to)).Returns(testAuditLogs);

            //act
            var result = _reportingService.GetAuditLog(from, to);

            //assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Data, Is.Not.Null);
                Assert.That(result.Data, Has.Count.EqualTo(2));
            });
        }

        //Summary Report Tests

        [Test]
        [Category("Positive")]
        public void GeneratingAValidSummaryReport()
        {
            //Arrange
            var testAccounnts = new List<Account>
            {
                new Account
                {
                    Id = 1, 
                    Status = AccountStatus.Active,
                    Balance = 2500m,
                },
                new Account
                {
                    Id = 2,
                    Status = AccountStatus.Active,
                    Balance = 500m
                },
                new Account
                {
                    Id = 3,
                    Status = AccountStatus.Dormant,
                    Balance = 250m
                },
                new Account
                {
                    Id = 4,
                    Status = AccountStatus.Closed,
                    Balance = 50000m
                }
            };

            _accountRepositoryMock.Setup(repo => repo.GetAll()).Returns(testAccounnts);

            DateTime reportDate = new DateTime(2026, 7, 3);

            //act
            var result = _reportingService.GenerateSummaryReport(reportDate);

            //Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Data, Does.Contain("Total Accounts  : 4"));
                Assert.That(result.Data, Does.Contain("Active          : 2"));
                Assert.That(result.Data, Does.Contain("Closed          : 1"));
                Assert.That(result.Data, Does.Contain("Dormant         : 1"));

                string correctFormatting = result.Data.Replace(" ", "").Replace("\u00A0", "");
                Assert.That(correctFormatting, Does.Contain("3250,00").Or.Contain("3250.00"));
            });
        }
    }
}


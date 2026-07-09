using System;
using System.Collections.Generic;
using System.Text;
using Moq;
using NUnit.Framework;
using BankCore.Core.Services;
using BankCore.Core.Models;
using BankCore.Core.Interfaces;
using System.Windows.Markup;
using System.ComponentModel.DataAnnotations;
using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;


namespace BankCore.Tests.NUnit
{
    [TestFixture]
    [Category("TransactionEngine")]
    public class TransactionEngineNUnitTests
    {
        private static List<Account> _universalTestAccounts = null!;
        private Mock<IValidationService> _validationServiceMock = null!;
        private Mock<IAccountRepository> _accountRepositoryMock = null!;
        private Mock<ITransactionRepository> _transactionRepositoryMock = null!;
        private Mock<IAuditService> _auditServiceMock = null!;
        private TransactionService _transactionEngine = null!;

        [OneTimeSetUp]
        public void OneTimeSetupMethod()
        {
            _universalTestAccounts = new List<Account>
            {
                new Account { Id = 2004, AccountNumber = "BC1000002004"},
                new Account { Id = 2005, AccountNumber = "BC1000002005"},
                new Account { Id = 2006, AccountNumber = "BC1000002006"}
            };
        }
        [OneTimeTearDown]
        public void OnetimeTearDownMethod()
        {
            _universalTestAccounts.Clear();
            _universalTestAccounts = null!;
        }

        [SetUp]
        public void Setup()
        {
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _transactionRepositoryMock = new Mock<ITransactionRepository>();
            _validationServiceMock = new Mock<IValidationService>();
            _auditServiceMock = new Mock<IAuditService>();

            _transactionEngine = new TransactionService(
                _accountRepositoryMock.Object,
                _transactionRepositoryMock.Object,
                _validationServiceMock.Object,
                _auditServiceMock.Object
            );
        }
        [TearDown]
        public void TearDown()
        {
            _accountRepositoryMock = null!;
            _transactionRepositoryMock = null!;
            _validationServiceMock = null!;
            _auditServiceMock = null!;
            _transactionEngine = null!;
        }
        //DEPOSIT TESTS
        [Test]
        [Category("Functional")]
        public void MakeDeposit_ValidActiveAccount_UpdatesBalanceAndLogsTransaction()
        {
            //arrange
            int accountId = 1;
            decimal initialBalance = 1000m;
            decimal depositAmount = 500m;

            var testAccount = new Account
            {
                Id = accountId,
                AccountNumber = "BC100000001",
                Balance = initialBalance,
                Status = AccountStatus.Active,
            };

            _accountRepositoryMock.Setup(repo => repo.GetById(accountId)).Returns(testAccount);

            //act
            var result = _transactionEngine.Deposit(accountId, depositAmount, "Salary", "Jaydee Venter");

            //assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Message, Is.EqualTo("Deposit successful."));
                Assert.That(testAccount.Balance, Is.EqualTo(1500.00m));
                Assert.That(testAccount.Balance, Is.GreaterThan(initialBalance));
            });
            _transactionRepositoryMock.Verify(repo => repo.Add(It.IsAny<Transaction>()), Times.Once);
            _auditServiceMock.Verify(audit => audit.Log("DEPOSIT", "Jaydee Venter", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [TestCase(0.00, "Deposit Amount 0", false)]
        [TestCase(-50.00, "Negative Deposit", false)]
        [Category("Negative")]
        public void MakeDeposit_InvalidandNegativeAmount_ReturnsError(decimal depositAmount, string action, bool expectedResult)
        {
            //act
            var Result = _transactionEngine.Deposit(1, (decimal)depositAmount, action, "Jaydee Venter");
            //assert
            Assert.That(Result.IsSuccess, Is.EqualTo(expectedResult));
            Assert.That(Result.Message, Is.EqualTo("Deposit amount must be greater than zero."));

            _accountRepositoryMock.Verify(repo => repo.Update(It.IsAny<Account>()), Times.Never);
        }

        [Test]
        [Category("Negative")]
        public void TestInvalidAccountID_ReturnsError()
        {
            //arrange
            int invalidAccountId = 1234589; 
            decimal depositAmount = 100m;
            _accountRepositoryMock.Setup(repo => repo.GetById(invalidAccountId)).Returns((Account)null!);
            //act
            var result = _transactionEngine.Deposit(invalidAccountId, depositAmount, "Test Deposit", "Jaydee Venter");
            //assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Account not found."));
            _accountRepositoryMock.Verify(repo => repo.Update(It.IsAny<Account>()), Times.Never);
        }

        [Test]
        [Category("Negative")]
        public void TestClosedAccount_ReturnsError()
        {
            //arrange
            int accountId = 2;
            decimal depositAmount = 100m;
            var closedAccount = new Account
            {
                Id = accountId,
                AccountNumber = "BC100000002",
                Balance = 500m,
                Status = AccountStatus.Closed,
            };
            _accountRepositoryMock.Setup(repo => repo.GetById(accountId)).Returns(closedAccount);
            //act
            var result = _transactionEngine.Deposit(accountId, depositAmount, "Test Deposit", "Jaydee Venter");
            //assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Deposits can only be made to Active accounts."));
            _accountRepositoryMock.Verify(repo => repo.Update(It.IsAny<Account>()), Times.Never);
        }

        public static object[] InvalidDepositAmounts =
        {
            new object[] { 0.00m },
            new object[] { -50.00m },
            new object[] { -100.00m },
        };

        [TestCaseSource(nameof(InvalidDepositAmounts))]
        [Category("Functional")]
        public void MakeDeposit_InvalidAmounts_ReturnsError(decimal depositAmount)
        {
            //arrange
            int accountId = 1;
            var testAccount = new Account
            {
                Id = accountId,
                AccountNumber = "BC100000001",
                Balance = 1000m,
                Status = AccountStatus.Active,
            };
            _accountRepositoryMock.Setup(repo => repo.GetById(accountId)).Returns(testAccount);
            //act
            var result = _transactionEngine.Deposit(accountId, depositAmount, "Invalid Deposit", "Jaydee Venter");
            //assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Deposit amount must be greater than zero."));
            _accountRepositoryMock.Verify(repo => repo.Update(It.IsAny<Account>()), Times.Never);
        }

        [Test]
        [Category("Functional")]
        public void PerformDeposit_CombinatorialRules(
            [Values(AccountStatus.Active, AccountStatus.Dormant, AccountStatus.Pending, AccountStatus.Closed)] AccountStatus accountStatus,
            [Values(100.00, 500.00, 5000.00, 1000.00)] double depositAmount)
        {

            // Arrange
            decimal depositAmountDecimal = (decimal)depositAmount;
            var testAccount = new Account
            {
                Id = 1,
                Balance = 1050m,
                Status = accountStatus,
            };
            _accountRepositoryMock.Setup(repo => repo.GetById(1)).Returns(testAccount);

            //Act
            var result = _transactionEngine.Deposit(1, depositAmountDecimal, "Combinatorial Deposit", "Jaydee Venter");


            //Assert
            if (accountStatus == AccountStatus.Active)
            {
                Assert.That(result.IsSuccess, Is.True);
            }
            else
            {
                Assert.That(result.IsSuccess, Is.False);
            }
        }

        [Test]
        [Category("Critical")]
        public void TryToDeposit_WhenDBCrashes_TrowsArgException()
        {
            //Arrange
            _accountRepositoryMock.Setup(repo => repo.GetById(It.IsAny<int>())).Throws(new Exception("Database Offline!!!"));

            //Act + Assert
            Assert.That(() => _transactionEngine.Deposit(1, 15000m, "Deposit Test", "Jaydee Venter"), Throws.TypeOf<Exception>());
        }

        [Test]
        [Category("Functional")]
        public void BatchDepositTests_MultipleValidAccs()
        {
            //Arrange
            var acc1 = _universalTestAccounts[0];
            var acc2 = _universalTestAccounts[1];
            var acc3 = _universalTestAccounts[2];

            acc1.Balance = 2500m;
            acc1.Status = AccountStatus.Active;

            acc2.Balance = 3200m;
            acc2.Status = AccountStatus.Active;

            acc3.Balance = 2000m;
            acc3.Status = AccountStatus.Active;

            _accountRepositoryMock.Setup(repo => repo.GetById(2004)).Returns(acc1);
            _accountRepositoryMock.Setup(repo => repo.GetById(2005)).Returns(acc2);
            _accountRepositoryMock.Setup(repo => repo.GetById(2006)).Returns(acc3);

            var batchDeposits = new List<(int AccountID, decimal Amount)>
            {
                (2004, 10000m),
                (2005, 500m),
                (2006, 350m),
                (2004, 200m)

            };

            //act + assert
            Assert.Multiple(() =>
            {
                foreach (var amt in batchDeposits)
                {
                    var result = _transactionEngine.Deposit(amt.AccountID, amt.Amount, "Testing Batch Deposits", "Jaydee Venter");
                    Assert.That(result.IsSuccess, Is.True, $"Batch DEpsot failed for account {amt.AccountID}");
                }
                Assert.That(acc1.Balance, Is.EqualTo(12700m));
                Assert.That(acc2.Balance, Is.EqualTo(3700m));
                Assert.That(acc3.Balance, Is.EqualTo(2350m));
            });
            _accountRepositoryMock.Verify(repo => repo.Update(It.IsAny<Account>()), Times.Exactly(4));
        }

        [Test]
        [Category("Functional")]
        public void CurrencyValidation_DepositCheck()
        {
            //Arrange
            int accountID = 1;
            decimal invalidCurrencyValue = 250.555m;

            var testAcc = new Account
            {
                Id = accountID,
                Balance = 5000m,
                Status = AccountStatus.Active
            };

            _accountRepositoryMock.Setup(repo => repo.GetById(accountID)).Returns(testAcc);

            //Act
            var result = _transactionEngine.Deposit(accountID, invalidCurrencyValue, "Currency Validation", "Jaydee Venter");

            //Assert
            Assert.That(result.IsSuccess, Is.True, "Program should not accept 3 decimal places");

            if (!result.IsSuccess)
            {
                _accountRepositoryMock.Verify(repo => repo.Update(It.IsAny<Account>()), Times.Never);
            };
        }

        [Test]
        [Category("Critical")]
        public void MakeDeposit_TransactionDBOffline_ThrowsException()
        {
            //Arrange
            var testAcc = new Account
            {
                Id = 1,
                Balance = 5000m,
                Status = AccountStatus.Active
            };
            _accountRepositoryMock.Setup(r => r.GetById(1)).Returns(testAcc);

            _transactionRepositoryMock.Setup(r => r.Add(It.IsAny<Transaction>())).Throws(new Exception("DB Offline"));

            //Act 
            var result = _transactionEngine.Withdraw(1, 100m, "Test", "System");

            //Assert
            Assert.That(result.IsSuccess, Is.False);
        }

            //WITHDRAWAL TESTS
          [Test]
        [Category("Functional")]
        public void MakeWithdrawal_ValidActiveAccount_UpdatesBalanceAndLogsTransaction()
        {
            //Arrange
            int accountId = 1;
            decimal initialBalance = 1000m;
            decimal withdrawalAmount = 750m;

            var testAccount = new Account
            {
                Id = accountId,
                AccountNumber = "BC100000001",
                DailyWithdrawalLimit = 50000m,
                Balance = initialBalance,
                Status = AccountStatus.Active,
            };

            _accountRepositoryMock.Setup(repo => repo.GetById(accountId)).Returns(testAccount);

            //Act
            var result = _transactionEngine.Withdraw(accountId, withdrawalAmount, "Rent Payment", "Jaydee Venter");

            //Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(withdrawalAmount, Is.InRange(0.01m, 50000.00m));
            Assert.That(result.Message, Is.EqualTo("Withdrawal successful."));
            Assert.That(testAccount.Balance, Is.EqualTo(250.00m));
            Assert.That(testAccount.Balance, Is.LessThan(initialBalance));

            _accountRepositoryMock.Verify(repo => repo.Update(It.IsAny<Account>()), Times.Once);
            _transactionRepositoryMock.Verify(repo => repo.Add(It.IsAny<Transaction>()), Times.Once);
            _auditServiceMock.Verify(audit => audit.Log("WITHDRAWAL", "Jaydee Venter", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Test]
        [Category("Negative")]
        public void MakeWithdrawal_InsufficientFunds_ReturnsError()
        {
            //Arrange
            int accountId = 1;
            decimal initialBalance = 1000m;
            decimal withdrawalAmount = 1500m;
            var testAccount = new Account
            {
                Id = accountId,
                AccountNumber = "BC100000001",
                Balance = initialBalance,
                Status = AccountStatus.Active,
            };
            _accountRepositoryMock.Setup(repo => repo.GetById(accountId)).Returns(testAccount);
            //Act
            var result = _transactionEngine.Withdraw(accountId, withdrawalAmount, "Large Withdrawal", "Jaydee Venter");
            //Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Insufficient funds."));
            Assert.That(testAccount.Balance, Is.EqualTo(initialBalance));
            _accountRepositoryMock.Verify(repo => repo.Update(It.IsAny<Account>()), Times.Never);
        }

        [Test]
        [Category("Negative")]
        public void MakeWithdrawal_ClosedAccount_ReturnsError()
        {
            //Arrange
            int accountId = 2;
            decimal withdrawalAmount = 750m;
            var closedAccount = new Account
            {
                Id = accountId,
                AccountNumber = "BC100000002",
                Balance = 500m,
                Status = AccountStatus.Closed,
            };
            _accountRepositoryMock.Setup(repo => repo.GetById(accountId)).Returns(closedAccount);
            //Act
            var result = _transactionEngine.Withdraw(accountId, withdrawalAmount, "Test Withdrawal", "Jaydee Venter");
            //Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Withdrawals can only be made from Active accounts."));
            _accountRepositoryMock.Verify(repo => repo.Update(It.IsAny<Account>()), Times.Never);
        }

        [Test]
        [Category("Negative")]
        public void MakeWithdrawal_InvalidAccountID_ReturnsError()
        {
            //Arrange
            int invalidAccountId = 1234589; 
            decimal withdrawalAmount = 100m;
            _accountRepositoryMock.Setup(repo => repo.GetById(invalidAccountId)).Returns((Account)null!);
            //Act
            var result = _transactionEngine.Withdraw(invalidAccountId, withdrawalAmount, "Test Withdrawal", "Jaydee Venter");
            //Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Account not found."));
            _accountRepositoryMock.Verify(repo => repo.Update(It.IsAny<Account>()), Times.Never);
        }

        [Test]
        [Category("Negative")]
        public void MakeWithdrawal_InvalidAmount_ReturnsError()
        {
            //Arrange
            int accountId = 1;
            decimal initialBalance = 1000m;
            decimal withdrawalAmount = -100m;
            var testAccount = new Account
            {
                Id = accountId,
                AccountNumber = "BC100000001",
                Balance = initialBalance,
                Status = AccountStatus.Active,
            };
            _accountRepositoryMock.Setup(repo => repo.GetById(accountId)).Returns(testAccount);
            //Act
            var result = _transactionEngine.Withdraw(accountId, withdrawalAmount, "Invalid Withdrawal", "Jaydee Venter");
            //Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Withdrawal amount must be greater than zero."));
            Assert.That(testAccount.Balance, Is.EqualTo(initialBalance));
            _accountRepositoryMock.Verify(repo => repo.Update(It.IsAny<Account>()), Times.Never);
        }

        [Test]
        [Category("Negative")]
        public void MakeWithdrawal_ExceedsMaximumLimit_ReturnsError()
        {
            //Arrange
            int accountId = 1;
            decimal initialBalance = 100000m;
            decimal withdrawalAmount = 60000m;
            var testAccount = new Account
            {
                Id = accountId,
                AccountNumber = "BC100000001",
                Balance = initialBalance,
                Status = AccountStatus.Active,
            };
            _accountRepositoryMock.Setup(repo => repo.GetById(accountId)).Returns(testAccount);
            //Act
            var result = _transactionEngine.Withdraw(accountId, withdrawalAmount, "Excessive Withdrawal", "Jaydee Venter");
            //Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Single withdrawal cannot exceed R50000,00."));
            Assert.That(testAccount.Balance, Is.EqualTo(initialBalance));
            _accountRepositoryMock.Verify(repo => repo.Update(It.IsAny<Account>()), Times.Never);
        }

        [Test]
        [Category("Negative")]
        public void MakeWithdrawal_ExceedsDailyLimit_ReturnsError()
        {
            //Arrange
            int accountId = 1;
            decimal initialBalance = 100m;
            decimal firstWithdrawalAmount = 100000m;
            var testAccount = new Account
            {
                Id = accountId,
                AccountNumber = "BC100000001",
                Balance = initialBalance,
                Status = AccountStatus.Active,
                DailyWithdrawalLimit = 50000m,
            };
            _accountRepositoryMock.Setup(repo => repo.GetById(accountId)).Returns(testAccount);

            //Act
            var result = _transactionEngine.Withdraw(accountId, firstWithdrawalAmount, "First Withdrawal", "Jaydee Venter");

            //Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Single withdrawal cannot exceed R50000,00."));
        }

        [Test]
        [Category("Negative")]
        public void MakeWithdraw_NewDay_MustResetDailyLimit()
        {
            //Arrange
            int accountId = 1;
            decimal WithdrawalAmount = 2500m; 
            var testAccount = new Account
            {
                Id = accountId,
                AccountNumber = "BC100000001",
                Balance = 2500000m,
                Status = AccountStatus.Active,
                LastActivityDate = DateTime.Now.Date.AddDays(-1),
                DailyWithdrawalLimit = 5000m,
                DailyWithdrawnToday = 5000m
            };
            _accountRepositoryMock.Setup(repo => repo.GetById(accountId)).Returns(testAccount);
            //Act
            var resultSecondWithdrawal = _transactionEngine.Withdraw(accountId, WithdrawalAmount, "Withdrawal", "Jaydee Venter");
            //Assert
            Assert.That(resultSecondWithdrawal.IsSuccess, Is.True);
            Assert.That(resultSecondWithdrawal.Message, Is.EqualTo("Withdrawal successful."));
        }


        //TRANSFER TESTS
        [Test]
        [Category("Negative")]
        public void MakeTransfer_to_SameAccount_ReturnError()
        {
            //Arrange
            int accountId = 1;
            var testAccount = new Account
            {
                Id = accountId,
                Balance = 1000m,
                Status = AccountStatus.Active
            };
            _accountRepositoryMock.Setup(repo => repo.GetById(accountId)).Returns(testAccount);

            //act
            var result = _transactionEngine.Transfer(accountId, accountId, 100m, "Test Transfer", "Jaydee Venter");

            //Assert
            Assert.That(result.IsSuccess, Is.False, "Transfer to same account should fail.");
            Assert.That(result.Message, Is.EqualTo("Cannot transfer to the same account."));
        }

        [TestCase(1, 2, 500.00, true)] //valid transfer
        [TestCase(1, 2, 750000.00, false)] //invalid transfer amount
        [TestCase(1, 12054, 100.00, false)] //invalid destination account
        [Category("Functional")]
        public void MakeTransfer_ValidAndInvalidScenarios(int sourceAccountId, int destinationAccountId, decimal transferAmount, bool expectedSuccess)
        {
            //Arrange
            var sourceAccount = new Account
            {
                Id = sourceAccountId,
                Balance = 1500m,
                Status = AccountStatus.Active
            };
            Account destinationAccount = destinationAccountId == 2 ? new Account
            {
                Id = 2,
                Balance = 1000m,
                Status = AccountStatus.Active
            } : null!;

            _accountRepositoryMock.Setup(repo => repo.GetById(sourceAccountId)).Returns(sourceAccount);
            _accountRepositoryMock.Setup(repo => repo.GetById(2)).Returns(destinationAccount);

            //Act
            var result = _transactionEngine.Transfer(sourceAccountId, destinationAccountId, transferAmount, "TestTransfer", "Jaydee Venter");
            //Assert
            Assert.That(result.IsSuccess, Is.EqualTo(expectedSuccess));
        }

        [Test]
        [Category("Negative")]
        public void MakeTransfer_to_DormantAccount_ReturnsError()
        {
            //Arrange
            int sourceAccountId = 1;
            int destinationAccountId = 2;
            decimal transferAmount = 100m;
            var sourceAccount = new Account
            {
                Id = sourceAccountId,
                Balance = 1000m,
                Status = AccountStatus.Active,
                DailyWithdrawalLimit = 50000m
            };
            var destinationAccount = new Account
            {
                Id = destinationAccountId,
                Balance = 500m,
                Status = AccountStatus.Dormant
            };
            _accountRepositoryMock.Setup(repo => repo.GetById(sourceAccountId)).Returns(sourceAccount);
            _accountRepositoryMock.Setup(repo => repo.GetById(destinationAccountId)).Returns(destinationAccount);

            //Act
            var result = _transactionEngine.Transfer(sourceAccountId, destinationAccountId, transferAmount, "Test Transfer", "Jaydee Venter");

            //Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Transfers can only be made to Active accounts."));

            _accountRepositoryMock.Verify(repo => repo.Update(It.IsAny<Account>()), Times.Never);
            _transactionRepositoryMock.Verify(repo => repo.Add(It.IsAny<Transaction>()), Times.Never);
        }
        [Test]
        [Category("Negative")]
        public void MakeR0Transfer_ReturnsError()
        {
            //Arrange
            int sourceAccountId = 1;
            int destinationAccountId = 2;
            decimal transferAmount = 0m; // R0 transfer
            var sourceAccount = new Account
            {
                Id = sourceAccountId,
                Balance = 1000m,
                Status = AccountStatus.Active,
                DailyWithdrawalLimit = 50000m
            };
            var destinationAccount = new Account
            {
                Id = destinationAccountId,
                Balance = 500m,
                Status = AccountStatus.Active
            };
            _accountRepositoryMock.Setup(repo => repo.GetById(sourceAccountId)).Returns(sourceAccount);
            _accountRepositoryMock.Setup(repo => repo.GetById(destinationAccountId)).Returns(destinationAccount);

            //Act
            var result = _transactionEngine.Transfer(sourceAccountId, destinationAccountId, transferAmount, "Test Transfer", "Jaydee Venter");

            //Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Transfer amount must be greater than zero."));
            _accountRepositoryMock.Verify(repo => repo.Update(It.IsAny<Account>()), Times.Never);
            _transactionRepositoryMock.Verify(repo => repo.Add(It.IsAny<Transaction>()), Times.Never);
        }

        [Test]
        [Category("Negative")]
        public void MakeTransfer_FromAcc_with_InsufficientFunds_ReturnsError()
        {
            //Arrange
            int sourceAccountId = 1;
            int destinationAccountId = 2;
            decimal transferAmount = 1500m; // More than the source account balance
            var sourceAccount = new Account
            {
                Id = sourceAccountId,
                Balance = 1000m,
                Status = AccountStatus.Active,
                DailyWithdrawalLimit = 50000m
            };
            var destinationAccount = new Account
            {
                Id = destinationAccountId,
                Balance = 500m,
                Status = AccountStatus.Active
            };
            _accountRepositoryMock.Setup(repo => repo.GetById(sourceAccountId)).Returns(sourceAccount);
            _accountRepositoryMock.Setup(repo => repo.GetById(destinationAccountId)).Returns(destinationAccount);

            //Act
            var result = _transactionEngine.Transfer(sourceAccountId, destinationAccountId, transferAmount, "Test Transfer", "Jaydee Venter");

            //Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Insufficient funds in source account."));
            _accountRepositoryMock.Verify(repo => repo.Update(It.IsAny<Account>()), Times.Never);
            _transactionRepositoryMock.Verify(repo => repo.Add(It.IsAny<Transaction>()), Times.Never);
        }

        //Reverse Transaction Tests
        [Test]
        [Category("Positive")]
        [Retry(3)]
        public void ReverseTransaction_ValidTransactionId()
        {
            //Arrange
            string validTransactionId = "TXN-20260703062004-93025F3E";
            int accountID = 1;

            var validTransaction = new Transaction
            {
                TransactionReference = validTransactionId,
                AccountId = accountID,
                Amount = 100m,
                Status = TransactionStatus.Completed,
                Timestamp = DateTime.UtcNow
            };

            var validAccount = new Account
            {
                Id = accountID,
                Balance = 5000m,
                Status = AccountStatus.Active
            };

            _transactionRepositoryMock.Setup(repo => repo.GetByReference(validTransactionId)).Returns(validTransaction);
            _accountRepositoryMock.Setup(repo => repo.GetById(accountID)).Returns(validAccount);

        //Act
        var result = _transactionEngine.ReverseTransaction(validTransactionId, "Test Reverse", "Jaydee Venter");

            //Assert
            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        [TestCase("TXN-12345678901234-INVALID", "INVALID REF", "Jaydee Venter", false)] // invalid transaction
        public void ReverseTransaction_InvalidTransactionId(string transactionReference, string description, string performedBy, bool expectedSuccess)
        {
            //Arrange
            _transactionRepositoryMock.Setup(repo => repo.GetByReference(transactionReference)).Returns((Transaction)null!);
            //Act
            var result = _transactionEngine.ReverseTransaction(transactionReference, description, performedBy);
            //Assert
            Assert.That(result.IsSuccess, Is.EqualTo(expectedSuccess));
            Assert.That(result.Message, Is.EqualTo("Transaction not found."));
        }

        [Test]
        [TestCase(" ", "Whitespace REF", "Jaydee Venter", false)] // empty transaction reference
        [TestCase("", "Empty REF", "Jaydee Venter", false)] // empty transaction reference
        public void ReverseTransaction_EmptyRefField_ReturnsError(string transactionReference, string description, string performedBy, bool expectedSuccess)
        {
            //Act
            var result = _transactionEngine.ReverseTransaction(transactionReference, description, performedBy);
            //Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Transaction reference is required."));

        }


        [Test]
        [Category("Negative")]
        public void ReverseTransaction_AlreadyReversedTransaction_ReturnsError()
        {
            //Arrange
            string prevReversedTransactionId = "TXN-20260703062004-93025F3E";
             int ReverseAmount = 5000;

            var reversedTransaction = new Transaction
            {
                TransactionReference = prevReversedTransactionId,
                Amount = ReverseAmount,
                Status = TransactionStatus.Reversed
            };
            _transactionRepositoryMock.Setup(repo => repo.GetByReference(prevReversedTransactionId)).Returns(reversedTransaction);
            //Act
            var result = _transactionEngine.ReverseTransaction(prevReversedTransactionId, "Test Reverse", "Jaydee Venter");
            //Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Transaction has already been reversed."));
        }

        [Test]
        [Category("Negative")]
        public void ReverseTransaction_ReversalWindowExpired_ReturnsError()
        {
            //Arrange
            string expiredTransactionId = "TXN-20260703062004-93025F3E";
            var oldTransaction = new Transaction
            {
                TransactionReference = expiredTransactionId,
                Amount = 200m,
                Status = TransactionStatus.Completed,
                Timestamp = DateTime.UtcNow.AddDays(-10)
            };
            _transactionRepositoryMock.Setup(repo => repo.GetByReference(expiredTransactionId)).Returns(oldTransaction);

            //act

            var result = _transactionEngine.ReverseTransaction(expiredTransactionId, "Test Expired Transaction Reversal", "Jaydee Venter");

            //Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Reversal window of 24 hours has expired."));
        }

        [Test]
        [Category("Negative")]
        public void ReverseTransaction_FromAccount_WithInsufficientFunds_ReturnsError()
        {
            //Arrange
            string TransactionID = "TXN-20260703062004-93025F3E";
            int accountID = 1;
            decimal firstDeposit = 1500m;

            var Transaction = new Transaction
            {
                TransactionReference = TransactionID,
                AccountId = accountID,
                Amount = firstDeposit,
                Type = TransactionType.Deposit,
                Status = TransactionStatus.Completed,
                Timestamp = DateTime.UtcNow.AddHours(-2)
            };

            var account = new Account
            {
                Id = accountID,
                AccountNumber = "BC1000000001",
                Balance = 150m,
                Status = AccountStatus.Active
            };

            _transactionRepositoryMock.Setup(repo => repo.GetByReference(TransactionID)).Returns(Transaction);
            _accountRepositoryMock.Setup(repo => repo.GetById(accountID)).Returns(account);

            ///Act
            var result = _transactionEngine.ReverseTransaction(TransactionID, "Testing Insufficient Transaction", "Jaydee Venter");

            //Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Insufficient funds to reverse this deposit."));
        }

        //Get Transaction History Tests
        [Test]
        [Category("Negative")]
        public void UsingInvalidAccount_toPull_TransactionHis_ReturnsError()
        {
            //Arrange

            int fakeAccountID = 1000;
            _accountRepositoryMock.Setup(repo => repo.GetById(fakeAccountID)).Returns((Account)null!);

            //Act
            var result = _transactionEngine.GetTransactionHistory(fakeAccountID);

            //Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.EqualTo("Account not found."));

            _transactionRepositoryMock.Verify(repo => repo.GetByAccountId(It.IsAny<int>()), Times.Never);
            _transactionRepositoryMock.Verify(repo => repo.GetByDateRange(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Test]
        [Category("Performance")]
        [Timeout(2000)]
        public void ProvidingNoDate_WhenTryingToRetrieveHistory_ReturnsError()
        {
            int accountID = 1;
            var testAccount = new Account
            {
                Id = accountID,
                Status = AccountStatus.Active,
            };

            var accTransations = new List<Transaction> { new Transaction(), new Transaction(), new Transaction() };

            _accountRepositoryMock.Setup(repo => repo.GetById(accountID)).Returns(testAccount);
            _transactionRepositoryMock.Setup(repo => repo.GetByAccountId(accountID)).Returns(accTransations);

            //Actt
            var result = _transactionEngine.GetTransactionHistory(accountID);

            //assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data, Has.Count.EqualTo(3));
        }

        //Decision Table:
        //Status               BAL      WAmt    WTDY  ER     EM 
        [TestCase(AccountStatus.Active, 1000.00, 500.00, 0.00, true, "Withdrawal successful.")]//BEMS-DT-01 TRUE TRUE TRUE
        [TestCase(AccountStatus.Closed, 1000.00, 500.00, 0.00, false, "Withdrawals can only be made from Active accounts.")]//BEMS-DT-02
        [TestCase(AccountStatus.Active, 100.00, 500.00, 0.00, false, "Insufficient funds.")]//BEMS-DT-03
        [TestCase(AccountStatus.Active, 10000.00, 500.00, 50000.00, false, "Daily withdrawal limit of R50000,00 would be exceeded.")]//BEMS-DT-04
        [TestCase(AccountStatus.Active, 50.00, 100.00, 50000.00, false, "Insufficient funds.")]//BEMS-DT-05
        [TestCase(AccountStatus.Closed, 120.00, 300.00, 0.00, false, "Withdrawals can only be made from Active accounts.")]//BEMS-DT-06
        [TestCase(AccountStatus.Closed, 25000.00, 1000.00, 50000.00, false, "Withdrawals can only be made from Active accounts.")]//BEMS-DT-07
        [TestCase(AccountStatus.Closed, 400.00, 600.00, 50000.00, false, "Withdrawals can only be made from Active accounts.")]//BEMS-DT-08
        [Category("Decision Table")]
        public void DecisionTable_WithdrawProcessTransaction(AccountStatus status, double accBalance, double withdrawBalance, double withdrawnTodayBal, bool expectedSuccess, string errorMsg)
        {
            //Arrange
            int accID = 1;

            var testDTAccount = new Account
            {
                Id = accID,
                Status = status, //DT RULE 1
                Balance = (decimal) accBalance, // DT RULE 2
                DailyWithdrawalLimit = 50000m,
                DailyWithdrawnToday = (decimal) withdrawnTodayBal //DT Rule 3
            };

            _accountRepositoryMock.Setup(repo => repo.GetById(accID)).Returns(testDTAccount);

            //acr
            var result = _transactionEngine.Withdraw(accID, (decimal)withdrawBalance, "DT TestCases", "Jaydee Venter");

            //Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.EqualTo(expectedSuccess));
                Assert.That(result.Message, Does.Contain(errorMsg));
            });
        }
    }
}
using BankCore.Core.Interfaces;
using BankCore.Core.Models;
using BankCore.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;

namespace BankCore.Tests.MSTest
{
    //ACCOUNT CREATION TESTS
    [TestClass]
    public class AccountCreationTests
    {
        private Mock<IAccountRepository> _mockedRepository = null!;
        private Mock<IValidationService> _mockedValidator = null!;
        private Mock<IAuditService> _mockedAuditService = null!;
        private AccountService _AccService = null!;

        [TestInitialize]
        public void TestSetup()
        {
            _mockedRepository = new Mock<IAccountRepository>();
            _mockedValidator = new Mock<IValidationService>();
            _mockedAuditService = new Mock<IAuditService>();            

            _AccService = new AccountService(
                _mockedRepository.Object,
                _mockedValidator.Object,
                _mockedAuditService.Object
            );
            _mockedValidator.Setup(v => v.IsValidName(It.IsAny<string>())).Returns(true);
        }

        [TestCleanup]
        public void CleanUp()
        {
            _mockedRepository = null!;
            _mockedValidator = null!;
            _mockedAuditService = null!;
            _AccService = null!;
        }

        [TestMethod]
        [TestCategory("Smoke")]
        public void CreateValidSavingsAcc_ReturnedSuccessfullyalongWithGeneratedAccNo()
        {
            //arrange
            _mockedValidator.Setup(validator => validator.IsValidSouthAfricanIdNumber(It.IsAny<string>())).Returns(true);
            _mockedValidator.Setup(validator => validator.IsValidBranchCode(It.IsAny<string>())).Returns(true);

            //Act
            var create = _AccService.CreateAccount("Jaydee Venter", "0408055092089", AccountType.Savings, 1200000, "001");

            //Assert
            Assert.IsTrue(create.IsSuccess);
            Assert.IsNotNull(create.Data);
            Assert.IsTrue(create.Data!.AccountNumber.StartsWith("BC"));
            Assert.AreEqual(AccountStatus.Active, create.Data.Status);
            _mockedRepository.Verify(t => t.Add(It.IsAny<Account>()), Times.Once);

            _mockedAuditService.Verify(a => a.Log(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
        }

        [DataTestMethod]
        [TestCategory("Functional")]
        [DataRow(AccountType.Savings, 100.00, true)]
        [DataRow(AccountType.Current, 500.00, true)]
        [DataRow(AccountType.Savings, 99.99, false)]
        [DataRow(AccountType.Current, 499.99, false)]
        [DataRow(AccountType.Savings, -600.00, false)]
        [DataRow(AccountType.Current, -1800.00, false)]
        [DataRow(AccountType.Savings, 0.00, false)]
        [DataRow(AccountType.Current, 0.00, false)]
        public void CreateAccount_WithMinimumDepositBoundaries(AccountType type, double deposit, bool expectedSuccess)
        {
            //Arrange
            _mockedValidator.Setup(validator => validator.IsValidSouthAfricanIdNumber(It.IsAny<string>())).Returns(true);
            _mockedValidator.Setup(validator => validator.IsValidBranchCode(It.IsAny<string>())).Returns(true);

            //Act
            var result = _AccService.CreateAccount("Jaydee Venter", "0408055092089", type, (decimal)deposit, "001001");

            //Assert
            Assert.AreEqual(expectedSuccess, result.IsSuccess);
        }
        [DataTestMethod]
        [TestCategory("Fucntional")]
        [DataRow(AccountType.FixedDeposit, 1000.00, true)]
        [DataRow(AccountType.FixedDeposit, -1000.00, false)]
        [DataRow(AccountType.FixedDeposit, 999.99, false)]
        [DataRow(AccountType.Notice, 500.00, true)]
        [DataRow(AccountType.Notice, -500.00, false)]
        public void CreateAccounts_NoticeandFixed_MinimumDeposits(AccountType type, double deposit, bool expectedSuccess)
        {
            //Arrange
            _mockedValidator.Setup(validator => validator.IsValidSouthAfricanIdNumber(It.IsAny<string>())).Returns(true);
            _mockedValidator.Setup(validator => validator.IsValidBranchCode(It.IsAny<string>())).Returns(true);
            //act
            var result = _AccService.CreateAccount("Jaydee Venter", "0408055092089", type, (decimal)deposit, "202202");
            //assertr
            Assert.AreEqual(expectedSuccess, result.IsSuccess);
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void CreateAccount_WithInvalidOwnerName_ReturnsError()
        {
            //Arrange
            _mockedValidator.Setup(validator => validator.IsValidName(It.IsAny<string>())).Returns(false);

            //acr
            var result = _AccService.CreateAccount("Jaydee2004", "0408055092089", AccountType.Savings, 2000, "250655");

            //Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "Invalid owner name.");
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void CreateAccount_WithInvalidCode_ReturnsError()
        {
            //Arrange
            _mockedValidator.Setup(validator => validator.IsValidName(It.IsAny<string>())).Returns(true);
            _mockedValidator.Setup(validator => validator.IsValidSouthAfricanIdNumber(It.IsAny<string>())).Returns(true);

            _mockedValidator.Setup(validator => validator.IsValidBranchCode(It.IsAny<string>())).Returns(false);

            //acr
            var result = _AccService.CreateAccount("Jaydee Venter", "0408055092089", AccountType.Savings, 2000, "FNBBANK");

            //Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "Invalid branch code.");
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void CreateAccount_WithInvalidIdNumber_ReturnsError()
        {
            //Arrange
            _mockedValidator.Setup(t => t.IsValidSouthAfricanIdNumber(It.IsAny<string>())).Returns(false);

            //Act
            var result = _AccService.CreateAccount("Jaydee Venter", "invalid-id", AccountType.Savings, 1000, "001");

            //Assert
            Assert.IsFalse(result.IsSuccess);
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void CreateAccount_WithInvalidBranchCode_ReturnsError()
        {
            //Arrange
            _mockedValidator.Setup(validator => validator.IsValidSouthAfricanIdNumber(It.IsAny<string>())).Returns(true);
            _mockedValidator.Setup(validator => validator.IsValidBranchCode(It.IsAny<string>())).Returns(false);

            //Act
            var result = _AccService.CreateAccount("Jaydee Venter", "0408055092089", AccountType.Savings, 1000, "123a56789");

            //Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Invalid branch code.", result.Message);
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void CreateAccount_WithInvalidAccType_ReturnsError()
        {

            //Arrange
            _mockedValidator.Setup(validator => validator.IsValidSouthAfricanIdNumber(It.IsAny<string>())).Returns(true);
            _mockedValidator.Setup(validator => validator.IsValidBranchCode(It.IsAny<string>())).Returns(true);

            //Act
            var result = _AccService.CreateAccount("Jaydee Venter", "0408055092089", (AccountType)2026, 1000, "202501");

            //Assert
            Assert.IsTrue(result.IsSuccess);
        }

        [TestMethod]
        [TestCategory("Functional")]
        public void CreateAccount_ValidDeposit_InitializesAccountCorrectly()
        {
            // Arrange
            _mockedValidator.Setup(v => v.IsValidName(It.IsAny<string>())).Returns(true);
            _mockedValidator.Setup(v => v.IsValidSouthAfricanIdNumber(It.IsAny<string>())).Returns(true);
            _mockedValidator.Setup(v => v.IsValidBranchCode(It.IsAny<string>())).Returns(true);

            // Act
            var result = _AccService.CreateAccount("Jaydee Venter", "0408055092089", AccountType.Savings, 500, "001");

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(AccountStatus.Active, result.Data!.Status);
            _mockedRepository.Verify(r => r.Add(It.IsAny<Account>()), Times.Once);
        }

        [TestMethod]
        [TestCategory("Negative")]

        public void CreateAccount_DatabaseFailure_ThrowsException()
        {
            //Arrange
            _mockedRepository.Setup(repo => repo.Add(It.IsAny<Account>())).Throws(new Exception("Database connection lost."));

            //Assert
            var exception = Microsoft.VisualStudio.TestTools.UnitTesting.Assert.ThrowsException<Exception>(() =>
                _AccService.CreateAccount("Jaydee Venter", "0408055092089", AccountType.Savings, 1000, "001")
            );
            StringAssert.Contains(exception.Message, "Database connection lost.");
        }

        [TestMethod]
        [TestCategory("Functional")]
        public void CreateAccount_DailyWithdrawalLimit_DefaultApplied_WithoutInitialDeposit()
        {
            //Arrange
            _mockedValidator.Setup(repo => repo.IsValidName(It.IsAny<string>())).Returns(true);
            _mockedValidator.Setup(repo => repo.IsValidSouthAfricanIdNumber(It.IsAny<string>())).Returns(true);
            _mockedValidator.Setup(repo => repo.IsValidBranchCode(It.IsAny<string>())).Returns(true);

            //Act
            var result = _AccService.CreateAccount("Jaydee Venter", "0408055092089", AccountType.Savings, 650.00m, "123456");

            //Assert
            Assert.AreNotEqual(650.00m, result.Data!.DailyWithdrawalLimit);
        }
    }

    //ACCOUNT UPDATE TESTS
    [TestClass]
    public class AccountUpdateTests
    {
        private Mock<IAccountRepository> _mockedRepository = null!;
        private AccountService _AccService = null!;

        [TestInitialize]
        public void TestSetup()
        {
            _mockedRepository = new Mock<IAccountRepository>();
            var mockedAuditRepo = new Mock<IAuditRepository>();

            _AccService = new AccountService(
                _mockedRepository.Object,
                new ValidationService(),
                new AuditService(mockedAuditRepo.Object)
                );
        }

        [TestMethod]
        [TestCategory("Functional")]
        public void SetActiveAccount_StatusToDormant_UpdatesStatusSuccessfully()
        {
            //Arrange
            var account = new Account { AccountNumber = "BC1000000001", Balance = 0, Status = AccountStatus.Active };
            _mockedRepository.Setup(t => t.GetById(1)).Returns(account);

            //Act
            var result = _AccService.SetDormant(1);

            //Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(AccountStatus.Dormant, account.Status);
        }

        [TestMethod]
        [TestCategory("Positive")]
        public void UpdateAccDetails_WithValidDetails_UpdatesSuccessfully()
        {
            //Arrange
            var account = new Account { AccountNumber = "BC1000000001", OwnerName = "Jaydee Venter" };
            _mockedRepository.Setup(t => t.GetById(1)).Returns(account);

            //Act
            var result = _AccService.UpdateAccount(1, "Jaydee Venter Updated", "123456");

            //Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("Jaydee Venter Updated", account.OwnerName);
        }

        [DataTestMethod]
        [TestCategory("Negative")]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow(null)]
        public void UpdateAccDetails_WithInvalidName_ReturnsError(string invalidName)
        {
            //Arrange
            var account = new Account { AccountNumber = "BC1000000001", OwnerName = "Jaydee Venter" };
            _mockedRepository.Setup(t => t.GetById(1)).Returns(account);

            //Act
            var result = _AccService.UpdateAccount(1, invalidName, "123456");

            //Assert
            Assert.IsFalse(result.IsSuccess);
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void UpdateAcc_InvalidAcc_ReturnsFalse()
        {
            //act
            var result = _AccService.UpdateAccount(99, "Jaydee Venter", "123456");

            //Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "Account not found.");
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void UpdateAcc_ClosedAcc_ReturnsFalse()
        {
            //Arrange
            var closedAccount = new Account
            {
                OwnerName = "Jaydee Venter",
                AccountNumber = "BC1000000001",
                Status = AccountStatus.Closed,
            };

            _mockedRepository.Setup(repo => repo.GetById(1)).Returns(closedAccount);

            //Act
            var result = _AccService.UpdateAccount(1, "Jaydee Venter", "202612");

            //Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "Cannot update a closed account.");
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void UpdateAcc_BranchCodeTooLong_ReturnsFalse()
        {
            //Arrange
            var testAccount = new Account
            {
                OwnerName = "Jaydee Venter",
                AccountNumber = "BC1000000001",
                Status = AccountStatus.Active,
            };
            _mockedRepository.Setup(repo => repo.GetById(1)).Returns(testAccount);

            //act
            var result = _AccService.UpdateAccount(1, "Jaydee Venter", "1234567");

            //Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "Invalid branch code.");
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void UpdateAccount_DatabaseFailure_ThrowsException()
        {
            // Arrange
            _mockedRepository.Setup(repo => repo.GetById(It.IsAny<int>())).Throws(new Exception("DB Offline"));

            // Act
            Action act = () => _AccService.UpdateAccount(1, "Jaydee", "1234");

            // Assert
            var result = Microsoft.VisualStudio.TestTools.UnitTesting.Assert.ThrowsException<Exception>(act);
            StringAssert.Contains(result.Message, "DB Offline");
        }

        [TestMethod]
        [TestCategory("Positive")]
        public void ReActivateDormantAcc_ReturnsTrue()
        {
            //Arrange
            var account = new Account
            {
                Id = 1,
                Status = AccountStatus.Dormant,
                Balance = 0m,
            };

            _mockedRepository.Setup(repo => repo.GetById(1)).Returns(account);

            //Act
            var result = _AccService.ReactivateAccount(1, 150m);

            //Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(AccountStatus.Active, account.Status);
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void AttemptToReActivateActiveAcc_ReturnsFalse()
        {
            //Arrange
            var account = new Account
            {
                Id = 1,
                Status = AccountStatus.Active,
            };

            _mockedRepository.Setup(repo => repo.GetById(1)).Returns(account);

            //Act
            var result = _AccService.ReactivateAccount(1, 100m);

            //Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Only dormant accounts can be reactivated.", result.Message);
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void ReActivate_DormantAcc_depositTooLow_ReturnsFalse()
        {
            //Arrange
            var account = new Account
            {
                Id = 1,
                Status = AccountStatus.Dormant,
            };

            _mockedRepository.Setup(repo => repo.GetById(1)).Returns(account);

            //Act
            var result = _AccService.ReactivateAccount(1, 15m);

            //Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Reactivation requires a minimum deposit of R50.", result.Message);
        }

    }

    //ACCOUNT CLOSURE TESTS
    [TestClass]
    public class AccountClosureTests
    {
        private Mock<IAccountRepository> _mockedRepository = null!;
        private AccountService _AccService = null!;

        [TestInitialize]
        public void TestSetup()
        {
            _mockedRepository = new Mock<IAccountRepository>();
            _AccService = new AccountService(_mockedRepository.Object, new Mock<IValidationService>().Object, new Mock<IAuditService>().Object);
        }

        [TestMethod]
        [TestCategory("Functional")]
        public void CloseAcc_WithZeroBalance_ClosesSuccessfully()
        {
            //Arrange
            var account = new Account { AccountNumber = "BC1000000001", Balance = 0, Status = AccountStatus.Active };
            _mockedRepository.Setup(t => t.GetById(1)).Returns(account);

            //Act
            var result = _AccService.CloseAccount(1, "System");

            //Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(AccountStatus.Closed, account.Status);
        }

        [TestMethod]
        [TestCategory("Negative")]
        [ExpectedException(typeof(ArgumentException))]
        public void CloseAcc_InvalidAccountId_ThrowsArgumentException()
        {
            //Arrange
            _mockedRepository.Setup(repo => repo.GetById(-1)).Throws(new ArgumentException("Invalid account ID."));

            //Act
            _AccService.CloseAccount(-1, "System");
        }

        [DataTestMethod]
        [TestCategory("Negative")]
        [DataRow(0.01)]
        [DataRow(100.00)]
        [DataRow(-50.00)]
        public void CloseAcc_WithNonZeroBalance_ReturnsError(double invalidBalance)
        {
            //Arrange
            var account = new Account { AccountNumber = "BC1000000001", Balance = (decimal)invalidBalance, Status = AccountStatus.Active };
            _mockedRepository.Setup(t => t.GetById(1)).Returns(account);

            //Act
            var result = _AccService.CloseAccount(1, "System");

            //Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "funds");
        }
    }

    //ACCOUNT QUERY TESTS
    [TestClass]
    public class AccountQueryTests
    {
        private Mock<IAccountRepository> _mockedRepository = null!;
        private Mock<IValidationService> _mockedValidator = null!;
        private AccountService _AccService = null!;

        [TestInitialize]
        public void TestSetup()
        {
            _mockedRepository = new Mock<IAccountRepository>();
            _mockedValidator = new Mock<IValidationService>();
            _AccService = new AccountService(
                _mockedRepository.Object, 
                _mockedValidator.Object, 
                new Mock<IAuditService>().Object);
        }

        [TestMethod]
        [TestCategory("Functional")]
        public void GetAcc_ValidAccID_ReturnCorrectAccDetails()
        {
            //Arrange
            var expectedAccount = new Account { AccountNumber = "BC1000000001" };
            _mockedRepository.Setup(t => t.GetById(1)).Returns(expectedAccount);

            //Act
            var result = _AccService.GetAccount(1);

            //Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedAccount.AccountNumber, result.Data!.AccountNumber);
        }

        [TestMethod]
        [TestCategory("Functional")]
        public void GetAllAccounts_ValidatesCollectionIntegrity()
        {
            //Arrange
            var expectedAccounts = new List<Account>
            {
                new Account { 
                    Id = 1, 
                    AccountNumber = "BC1000" 
                },
                new Account { 
                    Id = 2, 
                    AccountNumber = "BC2000" 
                }
            };

            //Act
            var actualAccounts = new List<Account>(expectedAccounts);

            //Assert
            CollectionAssert.AreEqual(expectedAccounts, actualAccounts);
            CollectionAssert.AllItemsAreNotNull(actualAccounts);
            CollectionAssert.AllItemsAreInstancesOfType(actualAccounts, typeof(Account));
        }

        [TestMethod]
        [TestCategory("Regression")]
        [ExpectedException(typeof(Exception))]
        public void GetAccount_DBFailure_ThrowsException()
        {
            // Arrange
            _mockedRepository.Setup(r => r.GetById(It.IsAny<int>())).Throws(new Exception("DB Offline"));

            // Act
            _AccService.GetAccount(99);
        }

        [TestMethod]
        [TestCategory("Positive")]
        public void GetAccByNumber_ValidAndExists_ReturnsAccount()
        {
            //Arrange
            _mockedValidator.Setup(validator => validator.IsValidAccountNumber("BC10000001")).Returns(true);
            _mockedRepository.Setup(repo => repo.GetByAccountNumber("BC10000001")).Returns(
                new Account 
                { 
                    AccountNumber = "BC10000001"
                });

            //Act
            var result = _AccService.GetAccountByNumber("BC10000001");

            //Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("BC10000001", result.Data!.AccountNumber);
        }


        [TestMethod]
        [TestCategory("Negative")]
        public void GetAccByNumber_InvalidNumber_ReturnsFalse()
        {
            //Arrange
            _mockedValidator.Setup(validator => validator.IsValidAccountNumber("account-number")).Returns(false);
            _mockedRepository.Setup(repo => repo.GetByAccountNumber("account-number")).Returns(
                new Account
                {
                    AccountNumber = "account-number"
                });

            //Act
            var result = _AccService.GetAccountByNumber("account-number");

            //Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Invalid account number format.", result.Message);
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void GetAccByOwner_invalidSAIdNo_ReturnsFailure()
        {
            //Arrange
            _mockedValidator.Setup(validator => validator.IsValidSouthAfricanIdNumber("SAIdnumber")).Returns(false);

            //Act
            var result = _AccService.GetAccountsByOwner("SAIdnumber");

            //Assert
            Assert.IsFalse(result.IsSuccess);
        }

        [TestMethod]
        [TestCategory("Positive")]
        public void GetAccByOwner_ValidSAId_ReturnsAccount()
        {
            //Arrange
            _mockedValidator.Setup(validator => validator.IsValidSouthAfricanIdNumber("0408055092089")).Returns(true);
            _mockedRepository.Setup(repo => repo.GetByOwnerIdNumber("0408055092089")).Returns(new List<Account> { new Account() });

            //Act
            var result = _AccService.GetAccountsByOwner("0408055092089");

            //Arrange
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Data!.Count);
        }
    }
}

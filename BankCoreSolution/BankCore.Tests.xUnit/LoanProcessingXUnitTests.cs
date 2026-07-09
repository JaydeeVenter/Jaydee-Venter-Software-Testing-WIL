using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Moq;
using FluentAssertions;
using BankCore.Core.Services;
using BankCore.Core.Models;
using BankCore.Core.Interfaces;
using System.Runtime.CompilerServices;

namespace BankCore.Tests.xUnit
{
    public class ApplyForLoanTests
    {
        private readonly Mock<ILoanRepository> _loanRepositoryMock = null!;
        private readonly Mock<IAccountRepository> _accountRepositoryMock = null!;
        private readonly Mock<IAuditService> _auditServiceMock = null!;
        private readonly LoanService _loanServiceEngine = null!;

        public ApplyForLoanTests()
        {
            _loanRepositoryMock = new Mock<ILoanRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _auditServiceMock = new Mock<IAuditService>();

            _loanServiceEngine = new LoanService(_loanRepositoryMock.Object, _accountRepositoryMock.Object, _auditServiceMock.Object);
        }

        [Fact]
        [Trait("Category", "Negative")]
        public void ApplyingForLoan_UsingInvalidAccount_ReturnsError()
        {
            //Arrange
            _accountRepositoryMock.Setup(repo => repo.GetById(2004)).Returns((Account)null);

            //Act
            var result = _loanServiceEngine.ApplyForLoan(2004, LoanType.Home, 1500000, 84, 0.95m, 100000, 0, 750);

            //Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Be("Account not found.");
        }

        [Fact]
        [Trait("Category", "Negative")]
        public void ApplyForLoan_UsingClosedAccount_ReturnsError()
        {
            //Arrange
            _accountRepositoryMock.Setup(repo => repo.GetById(1)).Returns(new Account
            {
                Id = 1,
                Status = AccountStatus.Closed,
            });

            //Act
            var result = _loanServiceEngine.ApplyForLoan(1, LoanType.Personal, 15000, 12, 0.09m, 11500, 0, 680);

            //Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Be("Loan applications require an Active account.");
        }

        [Theory]
        [Trait("Category", "Negative")]
        [InlineData(-1250, 12, 0.15, "Loan amount must be positive.")]
        [InlineData(1500, 2, 0.15, "Loan term must be between 3 and 360 months.")]
        [InlineData(1500, 361, 0.15, "Loan term must be between 3 and 360 months.")]
        [InlineData(2000, 12, 1.00, "Interest rate must be between 0.01% and 40%.")]
        [InlineData(2000, 12, 0.41, "Interest rate must be between 0.01% and 40%.")]
        public void ApplyingForALoan_UsingInvalidInfo_ReturnsError(decimal loanAmount, int loanTerm, decimal annualRate, string expectedErrMessage)
        {
            //Arrange
            _accountRepositoryMock.Setup(repo => repo.GetById(1)).Returns(new Account
            {
                Id = 1,
                Status = AccountStatus.Active,
            });
            //Act
            var result = _loanServiceEngine.ApplyForLoan(1, LoanType.Personal, loanAmount, loanTerm, annualRate, 25000, 0, 700);

            //Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Be(expectedErrMessage);
        }

        [Fact]
        [Trait("Category", "Negative")]
        public void AttemptingToApplyForLoan_BelowRequiredCrediitScore_ReturnsError()
        {
            //Arrange
            int credScore = 599;
            _accountRepositoryMock.Setup(repo => repo.GetById(1)).Returns(new Account
            {
                Id = 1,
                Status = AccountStatus.Active
            });

            //Act
            var result = _loanServiceEngine.ApplyForLoan(1, LoanType.Vehicle, 500000, 60, 0.10m, 35000, 0, credScore);

            //Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Contain("Minimum credit score of 600 required. Applicant score");
        }

        [Fact]
        [Trait("Category", "Bug-010")]
        public void ApplyingForLoan_BUG010()
        {
            //Arrange
            int loanAmount = 100000;
            var loanTerm = 12;
            var annualRate = 0.10m;
            var income = 30000;
            var debt = 5000;
            int credScore = 700;
            _accountRepositoryMock.Setup(repo => repo.GetById(1)).Returns(new Account
            {
                Id = 1,
                Status = AccountStatus.Active,
            });

            //Act
            var result = _loanServiceEngine.ApplyForLoan(1, LoanType.Personal, loanAmount, loanTerm, annualRate, income, debt, credScore);

            //Assert
            result.IsSuccess.Should().BeFalse("Failed because true DTI including the new instalment exceeds 40%");
        }

        [Fact]
        [Trait("Category", "Positive")]
        public void ApplyingForLoan_SuccessfullyAddsToAuditLog()
        {
            //Arrange
            _accountRepositoryMock.Setup(repo => repo.GetById(1)).Returns(new Account
            {
                Id = 1,
                Status = AccountStatus.Active,
            });

            //Act
            var result = _loanServiceEngine.ApplyForLoan(1, LoanType.Home, 2500000, 60, 0.09m, 150000, 2000, 700);

            //Assert
            result.IsSuccess.Should().BeTrue();
            _loanRepositoryMock.Verify(repo => repo.Add(It.IsAny<Loan>()), Times.Once);
            _auditServiceMock.Verify(repo => repo.Log(
                "LOAN_APPLICATION", 
                "SYSTEM",
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);
        }
    }

    public class ApproveLoanTests
    {
        private readonly Mock<ILoanRepository> _loanRepositoryMock = null!;
        private readonly Mock<IAccountRepository> _accountRepositoryMock = null!;
        private readonly Mock<IAuditService> _auditServiceMock = null!;
        private readonly LoanService _loanServiceEngine = null!;

        public ApproveLoanTests()
        {
            _loanRepositoryMock = new Mock<ILoanRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _auditServiceMock = new Mock<IAuditService>();

            _loanServiceEngine = new LoanService(_loanRepositoryMock.Object, _accountRepositoryMock.Object, _auditServiceMock.Object);
        }

        [Fact]
        [Trait("Category", "Positive")]
        public void ApprovingValidLoan()
        {
            //Arrange
            var loan = new Loan
            {
                LoanReference = "LN-20260705-35ADD9",
                AccountId = 1,
                Status = LoanStatus.Pending,
                PrincipalAmount = 15000
            };
            var account = new Account
            {
                Id = 1,
                Balance = 65000,
                Status = AccountStatus.Active
            };

            _loanRepositoryMock.Setup(repo => repo.GetByReference("LN-20260705-35ADD9")).Returns(loan);
            _accountRepositoryMock.Setup(repo => repo.GetById(1)).Returns(account);

            //Act
            var result = _loanServiceEngine.ApproveLoan("LN-20260705-35ADD9", "Jaydee Venter");

            //Assert
            result.IsSuccess.Should().BeTrue();
            account.Balance.Should().Be(80000);
            loan.Status.Should().Be(LoanStatus.Active);
        }
        [Fact]
        [Trait("Category", "Positive")]
        public void ApproveForLoan_SuccessfullyAddsToAuditLog()
        {
            //Arrange
            string loanRef = "LN-20260705-35ADD9";
            var loan = new Loan
            {
                LoanReference = loanRef,
                AccountId = 1,
                Status = LoanStatus.Pending,
                PrincipalAmount = 180000m
            };

            _loanRepositoryMock.Setup(repo => repo.GetByReference(loanRef)).Returns(loan);
            _accountRepositoryMock.Setup(repo => repo.GetById(1)).Returns(new Account
            {
                Id = 1,
                Status = AccountStatus.Active,
                Balance = 25000m
            });

            //Act
            var result = _loanServiceEngine.ApproveLoan(loanRef, "Jaydee Venter");

            //Assert
            result.IsSuccess.Should().BeTrue();

            _loanRepositoryMock.Verify(repo => repo.Update(It.IsAny<Loan>()), Times.AtLeastOnce);
            _auditServiceMock.Verify(repo => repo.Log(
                "LOAN_APPROVED", 
                "Jaydee Venter", 
                It.IsAny<string>(), 
                It.IsAny<string>()), Times.Once);
        }
    }

    public class RejectLoanTests
    {
        private readonly Mock<ILoanRepository> _loanRepositoryMock = null!;
        private readonly Mock<IAccountRepository> _accountRepositoryMock = null!;
        private readonly Mock<IAuditService> _auditServiceMock = null!;
        private readonly LoanService _loanServiceEngine = null!;

        public RejectLoanTests()
        {
            _loanRepositoryMock = new Mock<ILoanRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _auditServiceMock = new Mock<IAuditService>();

            _loanServiceEngine = new LoanService(_loanRepositoryMock.Object, _accountRepositoryMock.Object, _auditServiceMock.Object);
        }

        [Fact]
        [Trait("Category", "Positive")]
        public void RejectLoanTest_UpdatesStatusToRejected()
        {
            //Arrange
            string loanRef = "LN-20260705-35ADD9";
            var rejectLoanTest = new Loan
            {
                LoanReference = loanRef,
                Status = LoanStatus.Pending,
            };
            _loanRepositoryMock.Setup(repo => repo.GetByReference(loanRef)).Returns(rejectLoanTest);

            //Act
            var result = _loanServiceEngine.RejectLoan(loanRef, "Risky Individual", "Jaydee Venter");

            //Assert
            result.IsSuccess.Should().BeTrue();
            rejectLoanTest.Status.Should().Be(LoanStatus.Rejected);
            _auditServiceMock.Verify(repo => repo.Log(
                "LOAN_REJECTED",
                "Jaydee Venter",
                It.IsAny<string>(),
                loanRef), Times.Once);
        }
        [Fact]
        [Trait("Category", "Negative")]
        public void RejectLoan_InvalidReference()
        {
            //Arrange
            string invalidRef = "INVALID-REF";

            _loanRepositoryMock.Setup(repo => repo.GetByReference(invalidRef)).Returns((Loan)null);

            //Act
            var result = _loanServiceEngine.RejectLoan(invalidRef, "FRAUD", "Jaydee Venter");

            //Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Contain("Loan not found.");
        }
    }

    public class GenerateRepaymentScheduleTests
    {
        private readonly Mock<ILoanRepository> _loanRepositoryMock = null!;
        private readonly Mock<IAccountRepository> _accountRepositoryMock = null!;
        private readonly Mock<IAuditService> _auditServiceMock = null!;
        private readonly LoanService _loanServiceEngine = null!;

        public GenerateRepaymentScheduleTests()
        {
            _loanRepositoryMock = new Mock<ILoanRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _auditServiceMock = new Mock<IAuditService>();

            _loanServiceEngine = new LoanService(_loanRepositoryMock.Object, _accountRepositoryMock.Object, _auditServiceMock.Object);
        }

        [Fact]
        [Trait("Category", "Positive")]
        public void GenerateRepaymentSchedule_MathematicallyCorrect_Over36Months()
        {
            //Arrange
            var loan = new Loan
            {
                LoanReference = "LN-20260705-35ADD9",
                Status = LoanStatus.Pending,
                PrincipalAmount = 100000m,
                InterestRate = 0.12m,
                TermMonths = 36,
                MonthlyInstalment = 3321.43m
            };
            _loanRepositoryMock.Setup(repo => repo.GetByReference("LN-20260705-35ADD9")).Returns(loan);

            //Act
            var result = _loanServiceEngine.ApproveLoan("LN-20260705-35ADD9", "Jaydee Venter");
            var loanSchedule = result.Data.RepaymentSchedule;

            //Assert
            loanSchedule.Should().NotBeNull();
            loanSchedule.Count.Should().Be(36);
            loanSchedule[35].ClosingBalance.Should().Be(0m);
        }

        [Fact]
        [Trait("Category", "Negative")]
        public void GenerateRepaymentSchedule_WithInvalidLoanStatus_ReturnsError()
        {
            //Arrange
            var testLoan = new Loan
            {
                LoanReference = "LN-20260504-35ADR9",
                Status = LoanStatus.Pending,
            };
            _loanRepositoryMock.Setup(repo => repo.GetByReference("LN-20260504-35ADR9")).Returns(testLoan);

            //Act
            var result = _loanServiceEngine.GenerateRepaymentSchedule("LN-20260504-35ADR9");

            //Assert
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Contain("Repayment schedule only available for approved loans.");
        }

    }

    public class ProcessRepaymentTests
    {
        private readonly Mock<ILoanRepository> _loanRepositoryMock = null!;
        private readonly Mock<IAccountRepository> _accountRepositoryMock = null!;
        private readonly Mock<IAuditService> _auditServiceMock = null!;
        private readonly LoanService _loanServiceEngine = null!;

        public ProcessRepaymentTests()
        {
            _loanRepositoryMock = new Mock<ILoanRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _auditServiceMock = new Mock<IAuditService>();

            _loanServiceEngine = new LoanService(_loanRepositoryMock.Object, _accountRepositoryMock.Object, _auditServiceMock.Object);
        }

        [Fact]
        [Trait("Category", "Negative")]
        public void ProcessPayment_NegativeAmount()
        {
            //Arrange
            _loanRepositoryMock.Setup(repo => repo.GetByReference("LN-20260705-35ADD9")).Returns(new Loan
            {
                Status = LoanStatus.Active
            });

            //Act
            var result = _loanServiceEngine.ProcessRepayment("LN-20260705-35ADD9", -60000, "Jaydee Venter");

            //Assert
            result.IsSuccess.Should().BeFalse();
        }

        [Fact]
        [Trait("Category", "Bug-011")]
        public void ProcessPayment_BalanceGoesNegative_BUG011()
        {
            //Arrange
            var validLoan = new Loan
            {
                Status = LoanStatus.Active,
                LoanReference = "LN-20260705-35ADD9",
                OutstandingBalance = 1000m
            };
            _loanRepositoryMock.Setup(repo => repo.GetByReference("LN-20260705-35ADD9")).Returns(validLoan);

            //Act
            var result = _loanServiceEngine.ProcessRepayment("LN-20260705-35ADD9", 1100m, "Jaydee Venter");

            //Assert
            result.Data.OutstandingBalance.Should().Be(0m, "A repayment of 1100 was made");
            result.Data.Status.Should().Be(LoanStatus.Settled);
        }

        [Fact]
        [Trait("Category", "Positive")]

        public void ProcessPayment_OnArrearsAccount_ShouldReactivate()
        {
            //Arrange
            var loan = new Loan
            {
                LoanReference = "LN-20260705-35ADD9",
                Status = LoanStatus.Arrears,
                OutstandingBalance = 12000m,
                ArrearsAmount = 1000m,
            };

            _loanRepositoryMock.Setup(repo => repo.GetByReference("LN-20260705-35ADD9")).Returns(loan);

            //Act
            var result = _loanServiceEngine.ProcessRepayment("LN-20260705-35ADD9", 1100m, "Jaydee Venter");

            //Assert
            result.Data.ArrearsAmount.Should().Be(0m);
            result.Data.Status.Should().Be(LoanStatus.Active);
        }
    }

    public class CalculateSettlementAmountTests
    {
        private readonly Mock<ILoanRepository> _loanRepositoryMock = null!;
        private readonly Mock<IAccountRepository> _accountRepositoryMock = null!;
        private readonly Mock<IAuditService> _auditServiceMock = null!;
        private readonly LoanService _loanServiceEngine = null!;

        public CalculateSettlementAmountTests()
        {
            _loanRepositoryMock = new Mock<ILoanRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _auditServiceMock = new Mock<IAuditService>();

            _loanServiceEngine = new LoanService(_loanRepositoryMock.Object, _accountRepositoryMock.Object, _auditServiceMock.Object);
        }

        [Fact]
        [Trait("Category", "Bug-012")]
        public void CalculateSettlement_WrongFeeUsed()
        {
            //Arrange
            var loan = new Loan
            {
                LoanReference = "LN-20260705-35ADD9",
                Status = LoanStatus.Active,
                PrincipalAmount = 180000m,
                OutstandingBalance = 18000m
            };

            _loanRepositoryMock.Setup(repo => repo.GetByReference("LN-20260705-35ADD9")).Returns(loan);

            //Act

            var result = _loanServiceEngine.CalculateSettlementAmount("LN-20260705-35ADD9");

            result.Data.Should().Be(18270m);
        }
    }

    public class SettleLoanTest
    {
        private readonly Mock<ILoanRepository> _loanRepositoryMock = null!;
        private readonly Mock<IAccountRepository> _accountRepositoryMock = null!;
        private readonly Mock<IAuditService> _auditServiceMock = null!;
        private readonly LoanService _loanServiceEngine = null!;

        public SettleLoanTest()
        {
            _loanRepositoryMock = new Mock<ILoanRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _auditServiceMock = new Mock<IAuditService>();

            _loanServiceEngine = new LoanService(_loanRepositoryMock.Object, _accountRepositoryMock.Object, _auditServiceMock.Object);
        }

        [Fact]
        [Trait("Category", "Positive")]
        public void SettleLoan_ValidLoan_ChangesToSettled()
        {
            //Arrange
            var loan = new Loan
            {
                LoanReference = "LN-20260705-35ADD9",
                Status = LoanStatus.Active,
                PrincipalAmount = 1000,
                OutstandingBalance = 1000,
            };

            _loanRepositoryMock.Setup(repo => repo.GetByReference("LN-20260705-35ADD9")).Returns(loan);

            //Act
            var result = _loanServiceEngine.SettleLoan("LN-20260705-35ADD9", "Jaydee Venter");

            //Assert
            result.IsSuccess.Should().BeTrue();
            result.Data.Status.Should().Be(LoanStatus.Settled);
            result.Data.OutstandingBalance.Should().Be(0);
        }
    }

    public class GetLoanTests
    {
        private readonly Mock<ILoanRepository> _loanRepositoryMock = null!;
        private readonly Mock<IAccountRepository> _accountRepositoryMock = null!;
        private readonly Mock<IAuditService> _auditServiceMock = null!;
        private readonly LoanService _loanServiceEngine = null!;

        public GetLoanTests()
        {
            _loanRepositoryMock = new Mock<ILoanRepository>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _auditServiceMock = new Mock<IAuditService>();

            _loanServiceEngine = new LoanService(_loanRepositoryMock.Object, _accountRepositoryMock.Object, _auditServiceMock.Object);
        }
        [Fact]
        [Trait("Category", "Positive")]
        public void GetLoan_WithValid_Reference()
        {
            //Arrange
            string loanRef = "LN-20260705-35ADD9";
            _loanRepositoryMock.Setup(repo => repo.GetByReference(loanRef)).Returns(new Loan
            {
                LoanReference = loanRef,
            });

            //Act 
            var result = _loanServiceEngine.GetLoan(loanRef);

            //Assert
            result.IsSuccess.Should().BeTrue();
        }
        [Fact]
        [Trait("Category", "Negative")]
        public void GetLoan_WithInvalid_Reference()
        {
            //Arange
            string fakeLoanRef = "12345";
            _loanRepositoryMock.Setup(repo => repo.GetByReference("fake")).Returns((Loan)null);

            //act
            var result = _loanServiceEngine.GetLoan(fakeLoanRef);

            //Assertr
            result.IsSuccess.Should().BeFalse();
        }
    }
}

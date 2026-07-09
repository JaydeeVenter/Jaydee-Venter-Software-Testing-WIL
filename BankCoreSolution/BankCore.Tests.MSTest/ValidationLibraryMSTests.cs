using BankCore.Core.Interfaces;
using BankCore.Core.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace BankCore.Tests.MSTest
{
    [TestClass]
    public class ValidationLibraryMSTests
    {
        private ValidationService _validationService = null!;

        [TestInitialize]
        public void Setup()
        {
            _validationService = new ValidationService();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _validationService = null!;
        }

        [DataTestMethod]
        [TestCategory("Functional")]
        [DataRow("0408055092089", true)]
        [DataRow("040805509208", false)]
        [DataRow("04080550920891", false)]
        [DataRow("040805509208A", false)] 
        [DataRow("0408055092080", false)] 
        [DataRow("", false)]
        [DataRow(" ", false)] 
        public void ValidSAIDNumberTests_VariousInputs(string IDnumber, bool expectedResult)
        {
            //Act
            var result = _validationService.IsValidSouthAfricanIdNumber(IDnumber);
            //Assert
            Assert.AreEqual(expectedResult, result);
        }

        [DataTestMethod]
        [TestCategory("Functional")]
        [DataRow("1234567890123", false)]
        [DataRow("0000000000000", false)]
        public void ValidateSAIDNumber(string IDNumber, bool expectedResult)
        {
            //Arrange
            var IDValidator = new ValidationService();

            //act
            bool result = IDValidator.IsValidSouthAfricanIdNumber(IDNumber);

            //Assert
            Assert.AreEqual(expectedResult, result);
        }
        [TestMethod]
        [TestCategory("Functional")]
        public void IsValidSouthAfricanIdNumber_ExecutesLuhnAlgorithm()
        {
            string validSAId = "9914205000089";

            // Act
            bool result = _validationService.IsValidSouthAfricanIdNumber(validSAId);

            // Assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void DateValidationTest_InvalidMonth_ShouldReturnFalse()
        {
            // Act
            var result = _validationService.IsValidSouthAfricanIdNumber("0413055092089");
            // Assert
            Assert.IsFalse(result, "The ID number with an invalid month should return false.");
        }

        [DataTestMethod]
        [TestCategory("Positive")]
        [DataRow("BC1234567890", true, DisplayName = "Valid account number with 10 digits")]
        [DataRow("BC123456789", false, DisplayName = "Invalid account number with 9 digits")]
        public void CheckAccountNumberLength_VariousInputs(string TestaccountNumber, bool expectedResult)
        {
            // Act
            bool result = _validationService.IsValidAccountNumber(TestaccountNumber);
            // Assert
            Assert.AreEqual(expectedResult, result, $"Account number: {TestaccountNumber} should return {expectedResult}");
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void CheckValidAccNumber_NullorWhitespaces_returnsFalse()
        {
            //Assert
            Assert.IsFalse(_validationService.IsValidAccountNumber(""));
            Assert.IsFalse(_validationService.IsValidAccountNumber(" "));
            Assert.IsFalse(_validationService.IsValidAccountNumber(null!));
        }

        [DataTestMethod]
        [TestCategory("BVA")]
        [DataRow(0.01, true, DisplayName = "Valid, Minimum boundary")]
        [DataRow(999999.99, true, DisplayName = "Valid, Maximum boundary")]
        [DataRow(0.00, false, DisplayName = "Invalid, Below minimum")]
        [DataRow(1000000.00, false, DisplayName = "Invalid, Above maximum")] 
        public void CheckValidAmountBoundaries(double testAmount, bool expectedResult)
        {
            //ARRANGE
            decimal minAmount = 0.01m;
            decimal maxAmount = 999999.99m;
            decimal amount = (decimal)testAmount;

            //act
            bool result = _validationService.IsValidAmount(amount, minAmount, maxAmount);

            //assert
            Assert.AreEqual(expectedResult, result, $"Amount: {amount} should return {expectedResult}");
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void TestInvalidName_ShouldReturnFalse()
        {
            // Arrange
            string invalidName = "Jaydee123";
            // Act
            bool isValid = _validationService.IsValidName(invalidName);
            // Assert
            Assert.IsFalse(isValid, "The name with numbers should return false.");
        }


        [TestMethod]
        [TestCategory("Negative")]
        public void TestInvalidUsername_ShouldReturnFalse()
        {
            // Arrange
            string invalidUsername = "user@name";
            // Act
            bool isValid = _validationService.IsValidUsername(invalidUsername);
            // Assert
            Assert.IsFalse(isValid, "The username with special characters should return false.");
        }

        [DataTestMethod]
        [TestCategory("BVA")]
        [DataRow("J")]
        [DataRow("ThisNameWillDefinitelyBeTooLongRightMrAshellyMangenaYouAreTheBestThisNameShouldBeTooLongToBeAValidName")]
        public void TestInvalidName_Boundaries_ReturnsFalse(string invalidName)
        {
            //Assert
            Assert.IsFalse(_validationService.IsValidName(invalidName));
        }

        [DataTestMethod]
        [TestCategory("BVA")]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("jay")]
        [DataRow("jaydeeventerisathirdyearctustudent")]
        public void TestInvalidUsername_Boundaries_ReturnsTrue(string invalidUsername)
        {
            //Assert
            Assert.IsFalse(_validationService.IsValidUsername(invalidUsername));
        }


        [DataTestMethod]
        [TestCategory("BVA")]
        [DataRow("Password1!", true, DisplayName = "Valid password with all required characters")]
        [DataRow("password1!", false, DisplayName = "Invalid password, missing uppercase letter")]
        [DataRow("PASSWORD1!", false, DisplayName = "Invalid password, missing lowercase letter")]
        [DataRow("Password!", false, DisplayName = "Invalid password, missing digit")]
        [DataRow("Password1", false, DisplayName = "Invalid password, missing special character")]
        [DataRow("Pass1!", false, DisplayName = "Invalid password, too short")]
        [DataRow(" ", false, DisplayName = "Invalid password, whitespace only")]
        [DataRow("", false, DisplayName = "Invalid password, empty string")]

        public void TestPasswordValidation(string password, bool expectedResult)
        {
            // Act
            bool isValid = _validationService.IsValidPassword(password);
            // Assert
            Assert.AreEqual(expectedResult, isValid, $"Password: '{password}' validation result should be {expectedResult}.");
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void TestInvalidBranchcode_ShouldReturnFalse()
        {
            // Arrange
            string invalidBranchCode = "1234567";
            // Act
            bool isValid = _validationService.IsValidBranchCode(invalidBranchCode);
            // Assert
            Assert.IsFalse(isValid, "A branch code with more than 6 digits should return false.");
        }

        [DataTestMethod]
        [TestCategory("Negative")]
        [DataRow("jaydeeventer@icloud.com", true, DisplayName = "Valid email")]
        [DataRow("jaydeeventericloud.com", false, DisplayName = "Invalid email, missing @ and domain")]
        [DataRow("jaydeeventer@icloud", false, DisplayName = "Invalid email, missing domain extension")]
        [DataRow("jaydeeventer@.com", false, DisplayName = "Invalid email, missing domain name")]
        [DataRow("jaydeeventer@icloud..com", false, DisplayName = "Invalid email, double dot in domain")]
        [DataRow(" ", false, DisplayName = "Invalid email, whitespace only")]
        [DataRow("", false, DisplayName = "Invalid email, empty string")]
        public void EmailValidation_MultipleInputs_ShouldReturnExpectedResult(string email, bool expectedResult)
        {
            // Act
            bool isValid = _validationService.IsValidEmail(email);
            // Assert
            Assert.AreEqual(expectedResult, isValid, $"Email: '{email}' validation result should be {expectedResult}.");
        }

        [DataTestMethod]
        [TestCategory("Negative")]
        [DataRow("DROP TABLE Users;", false, DisplayName = "SQL Injection")]
        [DataRow("<script>alert(1)</script>", false, DisplayName = "Hits the < branch")]
        [DataRow("<body>", false, DisplayName = "Hits the > branch")]
        [DataRow("Safe User Input", true, DisplayName = "Safe input passes")]
        public void IsSafeInput_VariousInputs_TestsAllBranches(string input, bool expectedResult)
        {
            // Act
            bool isSafe = _validationService.IsSafeInput(input);

            // Assert
            Assert.AreEqual(expectedResult, isSafe);
        }
        [TestMethod]
        [TestCategory("Negative")]
        public void IsSafeInput_ContainsLessThan_ReturnsFalse()
        {
            //act
            bool result = _validationService.IsSafeInput("test<test");
            //assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void IsSafeInput_ContainsGreaterThan_ReturnsFalse()
        {
            //Act
            bool result = _validationService.IsSafeInput("test>test");
            //Arrange
            Assert.IsFalse(result);
        }
    }
}


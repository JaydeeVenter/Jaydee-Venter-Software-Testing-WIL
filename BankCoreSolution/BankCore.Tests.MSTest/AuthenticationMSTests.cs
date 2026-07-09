using BankCore.Core.Interfaces;
using BankCore.Core.Models;
using BankCore.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Bson;
using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

namespace BankCore.Tests.MSTest
{
    //LOGIN TESTS
    [TestClass]
    public class LoginTests
    {
        private Mock<IUserRepository> _mockedUserReposit = null!;
        private Mock<IPasswordHasher> _mockedPasswordHasher = null!;
        private AuthService _authorizationService = null!;

        public TestContext TestContext { get; set; } = null!;

        [TestInitialize]
        public void TestSetup()
        {
            _mockedUserReposit = new Mock<IUserRepository>();
            _mockedPasswordHasher = new Mock<IPasswordHasher>();
            var mockedAuditRepo = new Mock<IAuditService>();
            var mockedSessionRepo = new Mock<ISessionRepository>();
            var mockedValidation = new Mock<IValidationService>();

            _authorizationService = new AuthService(
                _mockedUserReposit.Object,
                mockedSessionRepo.Object,
                _mockedPasswordHasher.Object,
                mockedAuditRepo.Object,
                mockedValidation.Object
            );
        }

        [TestMethod]
        [TestCategory("Smoke")]
        public void Login_withValid_Details()
        {
            //Arrange
            string validPass = TestContext.Properties["ValidTestPassword"]!.ToString()!;
            var testSalt = "testSaltString";

            var user = new User 
            { 
                Id = 1, 
                Username = "admin", 
                PasswordHash = validPass, 
                Salt = testSalt, 
                Role = UserRole.Admin, 
                IsLocked = false 
            };

            _mockedUserReposit.Setup(repo => repo.GetByUsername("admin")).Returns(user);
            _mockedPasswordHasher.Setup(hasher => hasher.VerifyPassword(validPass, validPass, testSalt)).Returns(true);

            //Act
            var result = _authorizationService.Login("admin", validPass);

            //Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(user.Username, result.Data!.Username);
            _mockedUserReposit.Verify(repo => repo.GetByUsername("admin"), Times.Once);
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void Login_withLockedUser()
        {
            //Arrange
            string invalidPass = TestContext.Properties["InvalidTestPassword"]!.ToString()!;
            var user = new User 
            { 
                Id = 1,
                Username = "admin",
                PasswordHash = "dummyHashValue",
                Salt = "testSaltString",
                Role = UserRole.Admin,
                IsLocked = true 
            };
            _mockedUserReposit.Setup(repo => repo.GetByUsername(It.IsAny<string>())).Returns(user);

            //Act
            var result = _authorizationService.Login("admin", invalidPass);

            //Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "Account is locked");
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void Login_WithEmptyUsernameAndEmptyPassword()
        {
            //act
            var loginTest1 = _authorizationService.Login("", "HelloS1r!2026");
            var loginTest2 = _authorizationService.Login("Jaydee", " ");
            //Assertr
            Assert.IsFalse(loginTest1.IsSuccess);
            Assert.IsFalse(loginTest2.IsSuccess);
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void LoginWithInvalidUsername_ReturnsWithErrorMessage()
        {
            //Arrange
            string invalidPass = TestContext.Properties["InvalidTestPassword"]!.ToString()!;
            _mockedUserReposit.Setup(repo => repo.GetByUsername(It.IsAny<string>())).Returns((User)null!);

            //Act
            var result = _authorizationService.Login("InvalidUser", invalidPass);

            //Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "Invalid username or password");
        }

        [TestMethod]
        [TestCategory("Functional")]
        public void LogIn_WithLockedAccount_PastLockoutExpiry_ShouldAllowLogin()
        {
            //Arrange
            string validPass = TestContext.Properties["ValidTestPassword"]!.ToString()!;
            var testSalt = "testSaltString";
            var user = new User { Username = "admin", PasswordHash = validPass, Salt = testSalt, Role = UserRole.Admin, IsLocked = true, LockoutExpiry = DateTime.Now.AddMinutes(-10) };

            _mockedUserReposit.Setup(repo => repo.GetByUsername(It.IsAny<string>())).Returns(user);
            _mockedPasswordHasher.Setup(hasher => hasher.VerifyPassword(It.IsAny<string>(), user.PasswordHash, user.Salt)).Returns(true);

            //Act
            var result = _authorizationService.Login("admin", validPass);

            //Assert
            Assert.IsTrue(result.IsSuccess);
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void Login_IncrementFailAttempts()
        {
            //Arrabge
            var testUser = new User
            {
                Username = "admin",
                FailedLoginAttempts = 1,
                PasswordHash = "testHash",
                Salt = "testSalt",
            };
            _mockedUserReposit.Setup(repo => repo.GetByUsername("admin")).Returns(testUser);

            _mockedPasswordHasher.Setup(hash => hash.VerifyPassword(It.IsAny<string>(), "testHash", "testSalt"));

            //ACt
            _authorizationService.Login("admin", "WhatsThePassword?");

            //Assert
            Assert.AreEqual(2, testUser.FailedLoginAttempts);
        }

        [DataTestMethod]
        [DataRow("", DisplayName = "Empty password")]
        [DataRow(" ", DisplayName = "whitespace password")]
        [TestCategory("Negative")]
        public void Login_WithInvalidPassword_ShouldReturnError(string InvalidPasswordTest)
        {
            //Act
            var result = _authorizationService.Login("admin", InvalidPasswordTest);

            //Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "Username and password are required");
        }

        [DataTestMethod]
        [TestCategory("Negative")]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow(null)]
        public void ValidateSesh_NoToken_ReturnsError(string invalidToken)
        {
            //act
            var result = _authorizationService.ValidateSession(invalidToken);

            //Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "Token is required.");
        }

        [TestMethod]
        [Ignore("Pending MFA integration")]
        [TestCategory("Functional")]
        public void Login_WithMFAEnabled_ShouldPromptForCode()
        {
            //Assert
            Assert.Fail("This should never run.");
        }
        [TestMethod]
        [TestCategory("Negative")]
        public void Login_AccountIsLocked_ReturnsError()
        {
            // Arrange
            var lockedUser = new User 
            { 
                Username = "JaydeeVenter", 
                IsLocked = true 
            };

            _mockedUserReposit.Setup(repo => repo.GetByUsername("JaydeeVenter")).Returns(lockedUser);

            // Act
            var result = _authorizationService.Login("JaydeeVenter", "JaydeeVenter123!");

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Account is locked. Contact your administrator.", result.Message);
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void Login_ExceedsMaxAttempts_LocksAccount()
        {
            // Arrange:
            var user = new User 
            { 
                Username = "DivanVenter", 
                IsLocked = false, 
                FailedLoginAttempts = 2, 
                PasswordHash = "hash", 
                Salt = "salt" 
            };
            _mockedUserReposit.Setup(repo => repo.GetByUsername("DivanVenter")).Returns(user);
            _mockedPasswordHasher.Setup(hasher => hasher.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(false);

            // Act
            _authorizationService.Login("DivanVenter", "ILoveClerens@2026");

            // Assert
            Assert.IsTrue(user.IsLocked);
            _mockedUserReposit.Verify(repo => repo.Update(user), Times.Once);
        }

    }

    //SESSION TESTS
    [TestClass]
    public class SessionTests
    {
        private Mock<ISessionRepository> _mockedSessionReposit = null!;
        private AuthService _authorizationService = null!;

        [TestInitialize]
        public void TestSetup()
        {
            _mockedSessionReposit = new Mock<ISessionRepository>();
            _authorizationService = new AuthService(new Mock<IUserRepository>().Object, _mockedSessionReposit.Object, new Mock<IPasswordHasher>().Object, new Mock<IAuditService>().Object, new Mock<IValidationService>().Object);
        }

        [TestMethod]
        [TestCategory("Functional")]
        public void Logout_ValidToken_InvalidatesSession()
        {
            //Arrange
            var session = new Session { Token = "validToken_123", IsActive = true, UserId = 1 };
            _mockedSessionReposit.Setup(repo => repo.GetByToken("validToken_123")).Returns(session);

            //Act
            var result = _authorizationService.Logout("validToken_123");

            //Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(session.IsActive, "Session should end after logout.");
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void ValidateSession_TokenExpired_ReturnsError()
        {
            // Arrange
            var expiredSession = new Session { 
                Token = "expToken", 
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5) 
            };
            _mockedSessionReposit.Setup(r => r.GetByToken("expToken")).Returns(expiredSession);

            // Act
            var result = _authorizationService.ValidateSession("expToken");

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Session has expired.", result.Message);
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void ValidateSession_InvalidActibity()
        {
            //Arrange
            var expiredSession = new Session
            {
                Token = "expToken",
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                IsActive = false
            };
            _mockedSessionReposit.Setup(r => r.GetByToken("expToken")).Returns(expiredSession);

            // Act
            var result = _authorizationService.ValidateSession("expToken");

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Session has expired.", result.Message);
        }
    }

    //ROLE ACCESS TESTS
    [TestClass]
    public class RoleAccessTests
    {
        private Mock<ISessionRepository> _mockedSessionReposit = null!;
        private AuthService _authorizationService = null!;

        [TestInitialize]
        public void TestSetup()
        {
            _mockedSessionReposit = new Mock<ISessionRepository>();
            _authorizationService = new AuthService(new Mock<IUserRepository>().Object, _mockedSessionReposit.Object, new Mock<IPasswordHasher>().Object, new Mock<IAuditService>().Object, new Mock<IValidationService>().Object);
        }

        [TestMethod]
        [TestCategory("Functional")]
        public void HasPermission_WithValidRole_ReturnsTrue()
        {
            //Arrange
            var session = new Session { 
                Token = "validAdminToken", 
                Role = UserRole.Admin, 
                ExpiresAt = DateTime.UtcNow.AddMinutes(30), 
                IsActive = true 
            };
            _mockedSessionReposit.Setup(repo => repo.GetByToken("validAdminToken")).Returns(session);

            //Act
            bool result = _authorizationService.HasPermission(session.Token, "CREATE_ACCOUNT");

            //Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        [TestCategory("Functional")]
        public void HasPermissionWithExpiredSession_ShouldNotBeAllowed()
        {
            //Arrange
            var session = new Session { Token = "expiredToken", UserId = 1, Username = "admin", Role = UserRole.Admin, CreatedAt = DateTime.UtcNow.AddMinutes(-60), ExpiresAt = DateTime.UtcNow.AddMinutes(-30), IsActive = true };
            _mockedSessionReposit.Setup(repo => repo.GetByToken("expiredToken")).Returns(session);

            //Act
            bool result = _authorizationService.HasPermission(session.Token, "Admin");

            //Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("Functional")]
        public void HasPermission_RoleNotAutherised_ShouldNotBeAllowed()
        {
            var session = new Session 
            { 
                Token = "seshToken", 
                Role = UserRole.Teller
            };

            _mockedSessionReposit.Setup(repo => repo.GetByToken("seshToken")).Returns(session);

            // Act
            bool hasPerm = _authorizationService.HasPermission("seshToken", "APPROVE_LOAN");

            // Assert
            Assert.IsFalse(hasPerm);
        }
    }

    //PASSWORD POLICY TESTS
    [TestClass]
    public class PasswordPolicyTests
    {
        private Mock<IUserRepository> _mockedUserReposit = null!;
        private Mock<IPasswordHasher> _mockedPasswordHasher = null!;
        private Mock<ISessionRepository> _mockedSessionReposit = null!;
        private Mock<IValidationService> _mockedValidationService = null!;
        private AuthService _authorizationService = null!;

        public TestContext TestContext { get; set; } = null!;

        [TestInitialize]
        public void TestSetup()
        {
            _mockedUserReposit = new Mock<IUserRepository>();
            _mockedPasswordHasher = new Mock<IPasswordHasher>();
            _mockedSessionReposit = new Mock<ISessionRepository>();
            _mockedValidationService = new Mock<IValidationService>();

            _authorizationService = new AuthService(
                _mockedUserReposit.Object,
                _mockedSessionReposit.Object,
                _mockedPasswordHasher.Object,
                new Mock<IAuditService>().Object,
                new ValidationService());
        }

        private User SetupValidSession(string seshToken = "validToken", int userID = 1)
        {
            //Arrange
            var session = new Session
            {
                Token = seshToken,
                UserId = userID,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                IsActive = true,
            };
            var sessionUser = new User
            {
                Id = userID,
                Username ="admin",
                PasswordHash = "hasedPassword",
                Salt = "salt",
                PasswordHistory = new List<string>()
            };
            _mockedSessionReposit.Setup(repo => repo.GetByToken(seshToken)).Returns(session);
            _mockedUserReposit.Setup(repo => repo.GetById(userID)).Returns(sessionUser);
            return sessionUser;
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void PasswordPolicy_PasswordTooShort_ReturnsError()
        {
            //Arrange
            var user = SetupValidSession("validToken", 1);
            _mockedPasswordHasher.Setup(repo => repo.VerifyPassword(It.IsAny<string>(), user.PasswordHash, user.Salt)).Returns(true);
            _mockedValidationService.Setup(repo => repo.IsValidPassword("H3ll0!")).Returns(false);

            //Act
            var result = _authorizationService.ChangePassword("validToken", "ThisIsATestOldPassword!", "H3ll0!");

            //Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "complexity");
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void PasswordPolicy_MissingComplexity_ReturnsError()
        {
            //Arrange
            var user = SetupValidSession("validToken", 1);

            _mockedPasswordHasher.Setup(repo => repo.VerifyPassword("Admin@1234!", user.PasswordHash, user.Salt)).Returns(true);
            //Act
            var result = _authorizationService.ChangePassword("validToken", "Admin@1234!", "alllowercaseletters");

            //Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "New password does not meet complexity requirements.");
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void ChangePassword_InvalidCurrentPwrd_ReturnsError()
        {
            //Arrange
            var user = SetupValidSession();
            _mockedPasswordHasher.Setup(repo =>repo.VerifyPassword("invalidPass", user.PasswordHash, user.Salt)).Returns(false);

            //sact
            var result = _authorizationService.ChangePassword("validToken", "invalidPass", "Th!sIs@V3ryStr0ngP@ssw0rd");

            //Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "Current password is incorrect.");
        }
        [TestMethod]
        [TestCategory("Negative")]
        public void ChangePassword_InvalidUserID_ReturnsError()
        {

            //Arrange
            var session = new Session 
            { 
                Token = "validToken", 
                UserId = 2004, 
                ExpiresAt = DateTime.UtcNow.AddMinutes(30), 
                IsActive = true 
            };

            _mockedSessionReposit.Setup(repo => repo.GetByToken("validToken")).Returns(session);
            _mockedUserReposit.Setup(repo => repo.GetById(99)).Returns((User)null!);

            //Act
            var result = _authorizationService.ChangePassword("validToken", "ancientPass", "Blu3B3rryC0nn3ct!");

            //Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "User not found.");
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void ChangePassword_invalidNewPassword_ReturnsError()
        {
            var user = SetupValidSession();
            _mockedPasswordHasher.Setup(repo => repo.VerifyPassword("Admin@1234!", user.PasswordHash, user.Salt)).Returns(true);

            var result = _authorizationService.ChangePassword("validToken", "Admin@1234!", "CTU");

            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "does not meet complexity requirements.");
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void ChnagePassword_ReusePassword_returnsError()
        {
         //Arrange   
            var user = SetupValidSession();

            user.PasswordHistory = new List<string>
            {
                "reusedPassword"
            };

            _mockedPasswordHasher.Setup(hash => hash.VerifyPassword("Admin@1234!", user.PasswordHash, user.Salt)).Returns(true);
            _mockedPasswordHasher.Setup(hash => hash.HashPassword("N3wStr0ngP@ssword@2026!")).Returns(("reusedPassword", "newSalt"));

            //Act
            var result = _authorizationService.ChangePassword("validToken", "Admin@1234!", "N3wStr0ngP@ssword@2026!");

            //Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "New password cannot be the same as a recent password.");
        }

        [TestMethod]
        [TestCategory("Functiona")]
        public void ChangePasswoird_ValidInput()
        {
            //Arrange
            var user = SetupValidSession();

            user.PasswordHistory = new List<string>
            {
                "fakeHash"
            };

            _mockedPasswordHasher.Setup(hash => hash.VerifyPassword("Admin@1234!", user.PasswordHash, user.Salt)).Returns(true);
            _mockedPasswordHasher.Setup(hash => hash.HashPassword("H3ll0W0rld@123!")).Returns(("newHash", "newSalt"));

            //Act
            var result = _authorizationService.ChangePassword("validToken", "Admin@1234!", "H3ll0W0rld@123!");

            //Assert
            Assert.IsTrue(result.IsSuccess);
        }

    }
    //USER MANAGEMENT TESTS
    [TestClass]
    public class UserManagementTests
    {
        private Mock<IUserRepository> _mockedUserReposit = null!;
        private Mock<ISessionRepository> _mockedSessionReposit = null!;
        private Mock<IPasswordHasher> _mockedPasswordHasher = null!;
        private Mock<IAuditService> _mockedAuditService = null!;
        private Mock<IValidationService> _mockedValidationService = null!;
        private AuthService _authService = null!;

        [TestInitialize]
        public void testSetup()
        {
            _mockedUserReposit = new Mock<IUserRepository>();
            _mockedSessionReposit = new Mock<ISessionRepository>();
            _mockedPasswordHasher = new Mock<IPasswordHasher>();
            _mockedAuditService = new Mock<IAuditService>();
            _mockedValidationService = new Mock<IValidationService>();

            _authService = new AuthService(
                _mockedUserReposit.Object,
                _mockedSessionReposit.Object,
                _mockedPasswordHasher.Object,
                _mockedAuditService.Object,
                _mockedValidationService.Object);
        }

        [TestMethod]
        [TestCategory("Functional")]
        public void UserRegistration_ValidInput()
        {
            //Arrange
            _mockedValidationService.Setup(repo => repo.IsValidUsername("JaydeeVenter")).Returns(true);
            _mockedValidationService.Setup(repo => repo.IsValidPassword("Dun3Bugg!3z!")).Returns(true);
            _mockedPasswordHasher.Setup(repo => repo.HashPassword("Dun3Bugg!3z!")).Returns(("hash", "salt"));

            //Act
            var result = _authService.RegisterUser("JaydeeVenter", "Dun3Bugg!3z!", UserRole.Admin, "Admin");

            //Assert
            Assert.IsTrue(result.IsSuccess);
            _mockedUserReposit.Verify(repo => repo.Add(It.IsAny<User>()), Times.Once);
        }


        [TestMethod]
        [TestCategory("Functional")]
        public void LockAndUnlock_ValidUser_UpdatesStatus()
        {
            // Arrange
            var user = new User 
            { 
                Username = "CTUStudent0064" 
            };
            _mockedUserReposit.Setup(u => u.GetByUsername("CTUStudent0064")).Returns(user);

            // Act
            var lockResult = _authService.LockUser("CTUStudent0064", "Admin");

            // Assert
            Assert.IsTrue(lockResult.IsSuccess);
            Assert.IsTrue(user.IsLocked);

            // Act
            var unlockResult = _authService.UnlockUser("CTUStudent0064", "Admin");

            // Assert
            Assert.IsTrue(unlockResult.IsSuccess);
            Assert.IsFalse(user.IsLocked);
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void AttemptingToReregister_UserThatIsAlreadyActive_ReturnsFalse()
        {
            // Arrange
            string validUsername = "JaydeeVenter";
            string password = "TesterP@ssword1!";

            _mockedValidationService.Setup(v => v.IsValidUsername(validUsername)).Returns(true);
            _mockedValidationService.Setup(v => v.IsValidPassword(password)).Returns(true);

            _mockedUserReposit.Setup(u => u.UsernameExists(validUsername)).Returns(true);

            // Act
            var result = _authService.RegisterUser(validUsername, password, UserRole.Teller, "Admin");

            // Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "Username already exists.");
        }

        [TestMethod]
        [TestCategory("Negative")]
        public void Lougout_usingInvalToken_ReturnsFalse()
        {
            //act
            var result = _authService.Logout("InvalidSessionToken");

            //Assert
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "Session not found.");
        }

            [TestMethod]
        [TestCategory("Functional")]
        public void ChangePassword_ExecutesAllLogic()
        {
            // Arrange
            var user = new User
            {
                Id = 1,
                Username = "JaydeeV",
                PasswordHash = "testHash",
                Salt = "testSalt",
                PasswordHistory = new System.Collections.Generic.List<string>()
            };

            var session = new Session
            {
                UserId = 1,
                IsActive = true,
                ExpiresAt = DateTime.UtcNow.AddMinutes(60)
            };

            _mockedSessionReposit.Setup(sessionrepo => sessionrepo.GetByToken("Token")).Returns(session);
            _mockedUserReposit.Setup(repo => repo.GetById(1)).Returns(user);
            _mockedPasswordHasher.Setup(hasher => hasher.VerifyPassword("OldDummy", "testHash", "testSalt")).Returns(true);
            _mockedValidationService.Setup(validator => validator.IsValidPassword("NewDummy")).Returns(true);
            _mockedPasswordHasher.Setup(hasher => hasher.HashPassword("NewDummy")).Returns(("NewHash", "NewSalt"));

            // Act
            var result = _authService.ChangePassword("Token", "OldDummy", "NewDummy");

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("NewHash", user.PasswordHash);
            _mockedUserReposit.Verify(repo => repo.Update(user), Times.Once);
        }

    }
}


using BankCore.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace BankCore.Tests.MSTest
{
    [TestClass]
    public class PasswordHasherMSTests
    {
        private PasswordHasher _hasher = null!;

        [TestInitialize]
        public void Setup() => _hasher = new PasswordHasher();

        [TestMethod]
        public void HashAndVerify_ValidPassword()
        {
            //Arrange
            string password = "B3ttyE@tsCak3@ndUnlce53lls3ggs!";

            //Act
            var (hash, salt) = _hasher.HashPassword(password);

            
            bool isValid = _hasher.VerifyPassword(password, hash, salt);

            //Assert
            Assert.IsTrue(isValid);
        }
    }
}

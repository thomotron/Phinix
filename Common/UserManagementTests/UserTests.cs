using NUnit.Framework;
using UserManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UserManagement.Tests
{
    [TestFixture()]
    public class UserTests
    {
        [Test()]
        public void User_UsernameConstructor_ValidUsername_InstantiatedSuccessfully()
        {
            Assert.DoesNotThrow(() =>
            {
                User user = new User("Thomotron");
                Assert.That(user.Username == "Thomotron");
                Assert.That(user.Uuid != null);
            });
        }

        [Test()]
        public void User_UsernameConstructor_NullUsername_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                User user = new User(null);
            });
        }

        [Test()]
        public void User_UsernameConstructor_EmptyUsername_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                User user = new User("");
            });
        }

        [Test()]
        public void User_UuidConstructor_ValidUsernameAndUuid_InstantiatedSuccessfully()
        {
            Assert.DoesNotThrow(() =>
            {
                User user = new User("0f7d3089-4b7d-46df-8f2e-46cf7200298e", "Spiffy");
                Assert.That(user.Username == "Spiffy");
                Assert.That(user.Uuid == "0f7d3089-4b7d-46df-8f2e-46cf7200298e");
            });
        }

        [Test()]
        public void User_UuidConstructor_NullUsername_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                User user = new User("37dcdba4-e722-4f76-93d5-0b8b1a6c050c", null);
            });
        }

        [Test()]
        public void User_UuidConstructor_NullUuid_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                User user = new User(null, "SirTerryWrist");
            });
        }

        [Test()]
        public void User_UuidConstructor_EmptyUsernameAndUuid_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                User user = new User("", "");
            });
        }

        [Test()]
        public void User_UuidConstructor_NullUsernameAndUuid_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                User user = new User(null, null);
            });
        }
    }
}
using NUnit.Framework;
using System;
using System.IO;

namespace UserManagement.Tests
{
    [TestFixture()]
    public class UserManagerTests
    {
        [Test()]
        public void UserManager_ParameterlessConstructor_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                UserManager userManager = new UserManager();
            });
        }

        [Test()]
        public void Save_ValidFilePathFileDoesNotExist_SavedSuccessfully()
        {
            string filePath = "users";
            UserManager userManager = new UserManager();
            User userBuddy1913 = new User("Buddy1913");
            User userToddTheRodBodHoward = new User("ToddTheRodBodHoward");
            userManager.AddUser(userBuddy1913);
            userManager.AddUser(userToddTheRodBodHoward);

            Assert.DoesNotThrow(() =>
            {
                userManager.Save(filePath);
            });
            Assert.That(File.Exists(filePath));

            File.Delete(filePath);
        }

        [Test()]
        public void Save_ValidFilePathFileExists_SavedSuccessfully()
        {
            string filePath = "users";
            UserManager userManager = new UserManager();
            User userScooter = new User("scooter_outside_the_dairy");
            User userFleurieuMilkCo = new User("FleurieuMilkCo.");
            userManager.AddUser(userScooter);
            userManager.AddUser(userFleurieuMilkCo);
            File.Create(filePath).Close();

            Assert.That(File.Exists(filePath));
            Assert.DoesNotThrow(() =>
            {
                userManager.Save(filePath);
            });
            Assert.That(File.Exists(filePath));

            File.Delete(filePath);
        }

        [Test()]
        public void Save_EmptyFilePath_ThrowsArgumentException()
        {
            UserManager userManager = new UserManager();

            Assert.Throws<ArgumentException>(() =>
            {
                userManager.Save("");
            });
        }

        [Test()]
        public void Save_NullFilePath_ThrowsArgumentException()
        {
            UserManager userManager = new UserManager();

            Assert.Throws<ArgumentException>(() =>
            {
                userManager.Save(null);
            });
        }

        [Test()]
        public void Load_ValidFilePathFileExists_LoadedSuccessfully()
        {
            string filePath = "users";
            UserManager userManagerOne = new UserManager();
            User userOhHeyItsRaining = new User("OhHeyItsRaining");
            User userGoodStuffJonno = new User("GoodStuffJonno");
            string uuidOne = userOhHeyItsRaining.Uuid;
            string uuidTwo = userGoodStuffJonno.Uuid;
            userManagerOne.AddUser(userOhHeyItsRaining);
            userManagerOne.AddUser(userGoodStuffJonno);
            userManagerOne.Save(filePath);

            UserManager userManagerTwoElectricBoogaloo;
            Assert.DoesNotThrow(() =>
            {
                userManagerTwoElectricBoogaloo = UserManager.Load(filePath);

                Assert.That(userManagerTwoElectricBoogaloo.TryGetUser(uuidOne, out User loadedUserOne) == true);
                Assert.That(userManagerTwoElectricBoogaloo.TryGetUser(uuidTwo, out User loadedUserTwo) == true);

                Assert.That(loadedUserOne.Username == "OhHeyItsRaining");
                Assert.That(loadedUserTwo.Username == "GoodStuffJonno");
            });

            File.Delete(filePath);
        }

        [Test()]
        public void Load_ValidFilePathFileDoesNotExist_DoesNotThrow() // Difficult to compare with a blank instance without creating test-specific code
        {
            string filePath = "users";
            if (File.Exists(filePath)) File.Delete(filePath);

            Assert.DoesNotThrow(() =>
            {
                UserManager userManager = UserManager.Load(filePath);
            });
        }

        [Test()]
        public void Load_EmptyFilePath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                UserManager userManager = UserManager.Load("");
            });
        }

        [Test()]
        public void Load_NullFilePath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                UserManager userManager = UserManager.Load(null);
            });
        }

        [Test()]
        public void TryGetUser_ValidUuidUserExists_UserReturnedSuccessfully()
        {
            UserManager userManager = new UserManager();
            User userJohn = new User("llx_JohnAppleseedL337_xll");
            userManager.AddUser(userJohn);

            Assert.That(userManager.TryGetUser(userJohn.Uuid, out User returnedUser) == true);
            Assert.That(returnedUser == userJohn);
        }

        [Test()]
        public void TryGetUser_ValidUuidUserDoesNotExist_ReturnsFalse()
        {
            UserManager userManager = new UserManager();

            Assert.That(userManager.TryGetUser("cd203c4e-85c8-4d5b-a179-e08be96fdb65", out User returnedUser) == false);
        }

        [Test()]
        public void TryGetUser_EmptyUuid_ThrowsArgumentException()
        {
            UserManager userManager = new UserManager();

            Assert.Throws<ArgumentException>(() =>
            {
                userManager.TryGetUser("", out User returnedUser);
            });
        }

        [Test()]
        public void TryGetUser_NullUuid_ThrowsArgumentException()
        {
            UserManager userManager = new UserManager();

            Assert.Throws<ArgumentException>(() =>
            {
                userManager.TryGetUser(null, out User returnedUser);
            });
        }

        [Test()]
        public void AddUser_ValidUserDoesNotExist_ReturnsTrue()
        {
            UserManager userManager = new UserManager();
            User user = new User("RIPDickSmith");

            Assert.That(userManager.AddUser(user) == true);
        }

        [Test()]
        public void AddUser_ValidUserExists_ReturnsFalse()
        {
            UserManager userManager = new UserManager();
            User user = new User("RIPDickSmith");
            
            Assert.That(userManager.AddUser(user) == true);
            Assert.That(userManager.AddUser(user) == false);
        }

        [Test()]
        public void AddUser_NullUser_ThrowsArgumentNullException()
        {
            UserManager userManager = new UserManager();

            Assert.Throws<ArgumentNullException>(() =>
            {
                userManager.AddUser(null);
            });
        }

        [Test()]
        public void UpdateUser_ValidUserDoesNotExist_ReturnsFalse()
        {
            UserManager userManager = new UserManager();
            User user = new User("fleeting box of popcorn");

            Assert.That(userManager.UpdateUser(user) == false);
        }

        [Test()]
        public void UpdateUser_ValidUserExists_ReturnsTrue()
        {
            UserManager userManager = new UserManager();
            User oldUser = new User("GR33NFL4G5");
            string uuid = oldUser.Uuid;
            userManager.AddUser(oldUser);
            User newUser = new User(uuid, "GREENFLAGS");
            
            Assert.That(userManager.UpdateUser(newUser) == true);
            userManager.TryGetUser(uuid, out User returnedUser);
            Assert.That(returnedUser.Uuid == uuid);
            Assert.That(returnedUser.Username == "GREENFLAGS");
        }

        [Test()]
        public void UpdateUser_NullUser_ThrowsArgumentNullException()
        {
            UserManager userManager = new UserManager();

            Assert.Throws<ArgumentNullException>(() =>
            {
                userManager.UpdateUser(null);
            });
        }

        [Test()]
        public void RemoveUser_ValidUuidUserExists_ReturnsTrue()
        {
            UserManager userManager = new UserManager();
            User user = new User("Aaron Bloggs");
            string uuid = user.Uuid;
            userManager.AddUser(user);

            Assert.That(userManager.RemoveUser(uuid) == true);
        }

        [Test()]
        public void RemoveUser_ValidUuidUserDoesNotExist_ReturnsFalse()
        {
            UserManager userManager = new UserManager();

            Assert.That(userManager.RemoveUser("b5e0b05a-cdf4-4636-ac47-e2cb23c200dd") == false);
        }

        [Test()]
        public void RemoveUser_EmptyUuid_ThrowsArgumentException()
        {
            UserManager userManager = new UserManager();

            Assert.Throws<ArgumentException>(() =>
            {
                userManager.RemoveUser("");
            });
        }

        [Test()]
        public void RemoveUser_NullUuid_ThrowsArgumentException()
        {
            UserManager userManager = new UserManager();

            Assert.Throws<ArgumentException>(() =>
            {
                userManager.RemoveUser(null);
            });
        }
    }
}
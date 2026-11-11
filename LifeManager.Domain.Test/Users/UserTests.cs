using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Errors;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.Users
{
    public class UserTests
    {
        private static readonly DateTimeOffset CreationDate = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Create_ShouldReturnUser_WhenUserIsValid()
        {
            var userName = "test";
            var email = "r@email.com";
            var passwordHash = "$2a$10$abcdefghijklmnopqrstuvwxyz012345";
            var user = User.Create(userName, email, passwordHash, CreationDate).Value;

            Assert.NotNull(user);
            Assert.IsType<User>(user);
            Assert.Null(user.Id);
            Assert.Equal(userName, user.Name.Value);
            Assert.Equal(email, user.Email.Value);
            Assert.Equal(passwordHash, user.PasswordHash.Value);
            Assert.Equal(CreationDate, user.CreationDate);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenUserNameIsInvalid()
        {
            var result = User.Create(string.Empty, "r@email.com", "hash", CreationDate);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.UserNameIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenEmailIsInvalid()
        {
            var result = User.Create("test", "invalid-email", "hash", CreationDate);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.EmailIsInvalid, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenPasswordHashIsInvalid()
        {
            var result = User.Create("test", "r@email.com", string.Empty, CreationDate);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.PasswordHashIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void AssignId_ShouldSetId_WhenUserHasNoIdYet()
        {
            var user = User.Create("test", "r@email.com", "hash", CreationDate).Value;
            Assert.NotNull(user);

            user.AssignId(10);

            Assert.NotNull(user.Id);
            Assert.Equal(10, user.Id!.Value);
        }

        [Fact]
        public void ChangePassword_ShouldReplaceThePasswordHash()
        {
            var user = User.Create("test", "r@email.com", "old-hash", CreationDate).Value!;

            user.ChangePassword(PasswordHash.Create("new-hash").Value!);

            Assert.Equal("new-hash", user.PasswordHash.Value);
        }

        [Fact]
        public void Equals_ShouldReturnTrue_WhenUsersHaveTheSameId()
        {
            var user1 = User.Create("test1", "r1@email.com", "hash", CreationDate).Value!;
            var user2 = User.Create("test2", "r2@email.com", "hash", CreationDate).Value!;
            user1.AssignId(1);
            user2.AssignId(1);

            Assert.Equal(user1, user2);
            Assert.Equal(user1.GetHashCode(), user2.GetHashCode());
        }

        [Fact]
        public void Equals_ShouldReturnFalse_WhenUsersHaveDifferentIds()
        {
            var user1 = User.Create("test", "r@email.com", "hash", CreationDate).Value!;
            var user2 = User.Create("test", "r@email.com", "hash", CreationDate).Value!;
            user1.AssignId(1);
            user2.AssignId(2);

            Assert.NotEqual(user1, user2);
        }

        [Fact]
        public void Equals_ShouldReturnFalse_WhenNeitherUserHasBeenAssignedAnId()
        {
            var user1 = User.Create("test", "r@email.com", "hash", CreationDate).Value!;
            var user2 = User.Create("test", "r@email.com", "hash", CreationDate).Value!;

            Assert.NotEqual(user1, user2);
            Assert.Equal(user1, user1);
        }

        [Fact]
        public void Rename_ShouldTrimAndChangeTheName_WhenTheNameIsValid()
        {
            var user = User.Create("test", "r@email.com", "hash", CreationDate).Value!;

            var result = user.Rename("  Rafa  ");

            Assert.True(result.IsSuccess);
            Assert.Equal("Rafa", user.Name.Value);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Rename_ShouldKeepTheName_WhenTheNameIsBlank(string name)
        {
            var user = User.Create("test", "r@email.com", "hash", CreationDate).Value!;

            var result = user.Rename(name);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.UserNameIsNullOrWhiteSpace, result.Error);
            Assert.Equal("test", user.Name.Value);
        }

        [Fact]
        public void Rename_ShouldReturnTooLong_WhenTheNameHasMoreThan100Characters()
        {
            var user = User.Create("test", "r@email.com", "hash", CreationDate).Value!;

            var result = user.Rename(new string('a', 101));

            Assert.Equal(UserErrors.UserNameTooLong, result.Error);
        }

        [Fact]
        public void ScheduleDeletion_ShouldSetTheDateGraceDaysAhead_WhenNoneIsPending()
        {
            var user = User.Create("test", "r@email.com", "hash", CreationDate).Value!;
            var now = CreationDate.AddMonths(1);

            Assert.True(user.ScheduleDeletion(now));

            Assert.True(user.IsDeletionPending);
            Assert.Equal(now, user.DeletionRequestedAt);
            Assert.Equal(now.AddDays(AccountDeletion.GraceDays), user.DeletionScheduledFor);
        }

        [Fact]
        public void ScheduleDeletion_ShouldKeepTheFirstDate_WhenAskedAgain()
        {
            var user = User.Create("test", "r@email.com", "hash", CreationDate).Value!;
            user.ScheduleDeletion(CreationDate);

            Assert.False(user.ScheduleDeletion(CreationDate.AddDays(3)));

            Assert.Equal(CreationDate, user.DeletionRequestedAt);
            Assert.Equal(CreationDate.AddDays(AccountDeletion.GraceDays), user.DeletionScheduledFor);
        }

        [Fact]
        public void CancelDeletion_ShouldClearBothDates()
        {
            var user = User.Create("test", "r@email.com", "hash", CreationDate).Value!;
            user.ScheduleDeletion(CreationDate);

            user.CancelDeletion();

            Assert.False(user.IsDeletionPending);
            Assert.Null(user.DeletionRequestedAt);
            Assert.Null(user.DeletionScheduledFor);
        }
    }
}

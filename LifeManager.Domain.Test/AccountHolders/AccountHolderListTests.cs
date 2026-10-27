using LifeManager.Domain.AccountHolders;
using LifeManager.Domain.AccountHolders.Errors;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.AccountHolders
{
    public class AccountHolderListTests
    {
        private static readonly UserId UserId = new(1);

        [Fact]
        public void Create_ShouldKeepTheNamesInOrder_WhenTheyAreValid()
        {
            var result = AccountHolderList.Create(UserId, ["  Rafael Moreira Ferreira ", "Empresa X Ltda"]);

            Assert.True(result.IsSuccess);
            Assert.Equal(UserId, result.Value.UserId);
            Assert.Equal(["Rafael Moreira Ferreira", "Empresa X Ltda"], result.Value.Holders.Select(holder => holder.Name.Value));
            Assert.Equal([0, 1], result.Value.Holders.Select(holder => holder.Position));
            Assert.All(result.Value.Holders, holder => Assert.Equal(UserId, holder.UserId));
        }

        [Fact]
        public void Create_ShouldReturnAnEmptyList_WhenThereAreNoNames()
        {
            var result = AccountHolderList.Create(UserId, []);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value.Holders);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenTheNamesAreMissing()
        {
            var result = AccountHolderList.Create(UserId, null);

            Assert.False(result.IsSuccess);
            Assert.Equal(AccountHolderErrors.NamesRequired, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenThereAreMoreThanTheMaximumOfNames()
        {
            var names = Enumerable.Range(1, AccountHolderList.MaxHolders + 1).Select(number => $"Titular Numero {number}x").ToList();

            var result = AccountHolderList.Create(UserId, names);

            Assert.False(result.IsSuccess);
            Assert.Equal(AccountHolderErrors.TooMany, result.Error);
            Assert.Null(result.Error.Index);
        }

        [Fact]
        public void Create_ShouldAcceptTheMaximumOfNames()
        {
            var names = Enumerable.Range(1, AccountHolderList.MaxHolders).Select(number => $"Titular Numero {number}x").ToList();

            Assert.True(AccountHolderList.Create(UserId, names).IsSuccess);
        }

        [Theory]
        [InlineData("", "AccountHolder.NameIsNullOrWhiteSpace")]
        [InlineData("Ana de Sá", "AccountHolder.NameTooShort")]
        public void Create_ShouldPointToTheInvalidName(string invalidName, string code)
        {
            var result = AccountHolderList.Create(UserId, ["Rafael Moreira Ferreira", "Empresa X Ltda", invalidName]);

            Assert.False(result.IsSuccess);
            Assert.Equal(code, result.Error.Code);
            Assert.Equal(2, result.Error.Index);
        }

        [Theory]
        [InlineData("  JOSE DA SILVA ")]
        [InlineData("José  da   Silva")]
        public void Create_ShouldPointToTheSecondOccurrence_WhenANameRepeatsIgnoringAccentsCaseAndSpaces(string repeated)
        {
            var result = AccountHolderList.Create(UserId, ["José da Silva", "Empresa X Ltda", repeated]);

            Assert.False(result.IsSuccess);
            Assert.Equal(AccountHolderErrors.DuplicateName.Code, result.Error.Code);
            Assert.Equal(2, result.Error.Index);
        }
    }
}

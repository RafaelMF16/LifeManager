using LifeManager.Domain.AccountHolders.Errors;
using LifeManager.Domain.AccountHolders.ValueObjects;

namespace LifeManager.Domain.Test.AccountHolders
{
    public class AccountHolderNameTests
    {
        [Theory]
        [InlineData("Rafael Moreira Ferreira", "Rafael Moreira Ferreira")]
        [InlineData("  Empresa X Ltda  ", "Empresa X Ltda")]
        [InlineData("Maria Souza", "Maria Souza")]
        public void Create_ShouldReturnTheTrimmedName_WhenItHasAtLeastTwoNameWords(string value, string expected)
        {
            var result = AccountHolderName.Create(value);

            Assert.True(result.IsSuccess);
            Assert.Equal(expected, result.Value.Value);
        }

        [Fact]
        public void Create_ShouldNormalizeTheName_ForComparingHolders()
        {
            Assert.Equal("jose da silva", AccountHolderName.Create("  José  da   SILVA ").Value!.NormalizedValue);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_ShouldReturnFailure_WhenTheNameIsNullOrWhiteSpace(string? value)
        {
            var result = AccountHolderName.Create(value);

            Assert.False(result.IsSuccess);
            Assert.Equal(AccountHolderErrors.NameIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenTheNameIsTooLong()
        {
            var result = AccountHolderName.Create("Rafael " + new string('a', AccountHolderName.MaxLength));

            Assert.False(result.IsSuccess);
            Assert.Equal(AccountHolderErrors.NameTooLong, result.Error);
        }

        [Fact]
        public void Create_ShouldAcceptTheMaximumLength()
        {
            var name = "Rafael " + new string('a', AccountHolderName.MaxLength - "Rafael ".Length);

            Assert.True(AccountHolderName.Create(name).IsSuccess);
        }

        [Theory]
        [InlineData("Rafael")]
        [InlineData("Ana de Sá")]
        [InlineData("Jo da Silva")]
        [InlineData("Rafael da")]
        public void Create_ShouldReturnFailure_WhenTheNameHasLessThanTwoNameWords(string value)
        {
            var result = AccountHolderName.Create(value);

            Assert.False(result.IsSuccess);
            Assert.Equal(AccountHolderErrors.NameTooShort, result.Error);
        }
    }
}

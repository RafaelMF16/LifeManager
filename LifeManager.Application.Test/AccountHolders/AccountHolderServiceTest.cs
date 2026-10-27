using LifeManager.Application.AccountHolders.DTOs;
using LifeManager.Application.AccountHolders.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.AccountHolders.Errors;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.AccountHolders
{
    [Collection("ApplicationServices")]
    public class AccountHolderServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly UserId OtherUserId = new(2);

        private readonly AccountHolderService _accountHolderService;

        public AccountHolderServiceTest()
        {
            _accountHolderService = ServiceProvider.GetRequiredService<AccountHolderService>();

            AccountHolderSingleton.Instance.Clear();
        }

        [Fact]
        public async Task GetAsync_ShouldReturnAnEmptyList_WhenTheUserHasNoHolders()
        {
            var result = await _accountHolderService.GetAsync(UserId, CancellationToken.None);

            Assert.Empty(result.Names!);
        }

        [Fact]
        public async Task ReplaceAsync_ShouldSaveTheTrimmedNamesInOrder()
        {
            var result = await _accountHolderService.ReplaceAsync(new AccountHoldersDto([" Rafael Moreira Ferreira ", "Empresa X Ltda"]), UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(["Rafael Moreira Ferreira", "Empresa X Ltda"], result.Value.Names!);
            var stored = await _accountHolderService.GetAsync(UserId, CancellationToken.None);
            Assert.Equal(["Rafael Moreira Ferreira", "Empresa X Ltda"], stored.Names!);
        }

        [Fact]
        public async Task ReplaceAsync_ShouldReplaceTheWholeList()
        {
            await _accountHolderService.ReplaceAsync(new AccountHoldersDto(["Rafael Moreira Ferreira", "Empresa X Ltda"]), UserId, CancellationToken.None);

            await _accountHolderService.ReplaceAsync(new AccountHoldersDto(["Maria Souza Lima"]), UserId, CancellationToken.None);

            var stored = await _accountHolderService.GetAsync(UserId, CancellationToken.None);
            Assert.Equal(["Maria Souza Lima"], stored.Names!);
        }

        [Fact]
        public async Task ReplaceAsync_ShouldDeleteEveryHolder_WhenTheListIsEmpty()
        {
            await _accountHolderService.ReplaceAsync(new AccountHoldersDto(["Rafael Moreira Ferreira"]), UserId, CancellationToken.None);

            var result = await _accountHolderService.ReplaceAsync(new AccountHoldersDto([]), UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty((await _accountHolderService.GetAsync(UserId, CancellationToken.None)).Names!);
        }

        [Fact]
        public async Task ReplaceAsync_ShouldKeepTheStoredList_WhenANameIsInvalid()
        {
            await _accountHolderService.ReplaceAsync(new AccountHoldersDto(["Rafael Moreira Ferreira"]), UserId, CancellationToken.None);

            var result = await _accountHolderService.ReplaceAsync(new AccountHoldersDto(["Empresa X Ltda", "Rafa"]), UserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(AccountHolderErrors.NameTooShort.Code, result.Error.Code);
            Assert.Equal(1, result.Error.Index);
            Assert.Equal(["Rafael Moreira Ferreira"], (await _accountHolderService.GetAsync(UserId, CancellationToken.None)).Names!);
        }

        [Fact]
        public async Task ReplaceAsync_ShouldReturnFailure_WhenTheNamesAreMissing()
        {
            var result = await _accountHolderService.ReplaceAsync(new AccountHoldersDto(null), UserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(AccountHolderErrors.NamesRequired, result.Error);
        }

        [Fact]
        public async Task ReplaceAsync_ShouldOnlyChangeTheUsersOwnList()
        {
            await _accountHolderService.ReplaceAsync(new AccountHoldersDto(["Maria Souza Lima"]), OtherUserId, CancellationToken.None);

            await _accountHolderService.ReplaceAsync(new AccountHoldersDto(["Rafael Moreira Ferreira"]), UserId, CancellationToken.None);
            await _accountHolderService.ReplaceAsync(new AccountHoldersDto([]), UserId, CancellationToken.None);

            Assert.Empty((await _accountHolderService.GetAsync(UserId, CancellationToken.None)).Names!);
            Assert.Equal(["Maria Souza Lima"], (await _accountHolderService.GetAsync(OtherUserId, CancellationToken.None)).Names!);
        }
    }
}

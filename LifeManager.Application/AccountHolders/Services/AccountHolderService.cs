using LifeManager.Application.AccountHolders.DTOs;
using LifeManager.Domain.AccountHolders;
using LifeManager.Domain.AccountHolders.Interfaces;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.AccountHolders.Services
{
    public class AccountHolderService(IAccountHolderRepository accountHolderRepository)
    {
        private readonly IAccountHolderRepository _accountHolderRepository = accountHolderRepository;

        public async Task<AccountHoldersDto> GetAsync(UserId userId, CancellationToken cancellationToken)
        {
            var holders = await _accountHolderRepository.GetByUserIdAsync(userId, cancellationToken);

            return ToResponseDto(holders);
        }

        public async Task<Result<AccountHoldersDto>> ReplaceAsync(AccountHoldersDto accountHoldersDto, UserId userId, CancellationToken cancellationToken)
        {
            var listResult = AccountHolderList.Create(userId, accountHoldersDto.Names);
            if (!listResult.IsSuccess)
                return listResult.Error;

            await _accountHolderRepository.ReplaceAsync(listResult.Value, cancellationToken);

            return ToResponseDto(listResult.Value.Holders);
        }

        private static AccountHoldersDto ToResponseDto(IReadOnlyList<AccountHolder> holders)
            => new([.. holders.Select(holder => holder.Name.Value)]);
    }
}

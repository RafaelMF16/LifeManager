using LifeManager.Application.UsersPreferences.DTOs;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences;
using LifeManager.Domain.UsersPreferences.Interfaces;

namespace LifeManager.Application.UsersPreferences.Services
{
    public class UserPreferencesService(IUserPreferencesRepository userPreferencesRepository)
    {
        private readonly IUserPreferencesRepository _userPreferencesRepository = userPreferencesRepository;

        public async Task<UserPreferencesDto> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken)
        {
            var userPreferences = await _userPreferencesRepository.GetByUserIdAsync(userId, cancellationToken)
                ?? UserPreferences.CreateDefault(userId);

            return ToResponseDto(userPreferences);
        }

        public async Task<Result<UserPreferencesDto>> AddOrUpdateAsync(UserPreferencesDto userPreferencesDto, UserId userId, CancellationToken cancellationToken)
        {
            var userPreferences = await _userPreferencesRepository.GetByUserIdAsync(userId, cancellationToken);

            if (userPreferences is null)
            {
                var createResult = UserPreferences.Create(userId.Value, userPreferencesDto.Theme, userPreferencesDto.Language);
                if (!createResult.IsSuccess)
                    return createResult.Error;

                var addedPreferences = await _userPreferencesRepository.AddAsync(createResult.Value, cancellationToken);

                return ToResponseDto(addedPreferences);
            }

            var updateResult = userPreferences.Update(userPreferencesDto.Theme, userPreferencesDto.Language);
            if (!updateResult.IsSuccess)
                return updateResult.Error;

            await _userPreferencesRepository.UpdateAsync(userPreferences, cancellationToken);

            return ToResponseDto(userPreferences);
        }

        private static UserPreferencesDto ToResponseDto(UserPreferences userPreferences)
            => new(userPreferences.Theme, userPreferences.Language);
    }
}

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

        public UserPreferencesDto GetUserPreferencesByUserId(UserId userId)
        {
            var userPreferences = _userPreferencesRepository.GetUserPreferencesByUserId(userId)
                ?? UserPreferences.CreateDefault(userId);

            return ToResponseDto(userPreferences);
        }

        public Result<UserPreferencesDto> AddOrUpdate(UserPreferencesDto userPreferencesDto, UserId userId)
        {
            var userPreferences = _userPreferencesRepository.GetUserPreferencesByUserId(userId);

            if (userPreferences is null)
                return UserPreferences.Create(userId.Value, userPreferencesDto.Theme, userPreferencesDto.Language)
                    .Map(newUserPreferences => _userPreferencesRepository.Add(newUserPreferences))
                    .Map(ToResponseDto);

            return userPreferences.Update(userPreferencesDto.Theme, userPreferencesDto.Language)
                .Map(updatedUserPreferences => _userPreferencesRepository.Update(updatedUserPreferences))
                .Map(ToResponseDto);
        }

        private static UserPreferencesDto ToResponseDto(UserPreferences userPreferences)
            => new(userPreferences.Theme, userPreferences.Language);
    }
}
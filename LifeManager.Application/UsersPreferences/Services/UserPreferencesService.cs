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

        public UserPreferences GetUserPreferencesByUserId(UserId userId)
        {
            return _userPreferencesRepository.GetUserPreferencesByUserId(userId)
                ?? UserPreferences.CreateDefault(userId);
        }
        
        public Result<UserPreferences> AddOrUpdate(UserPreferencesDto userPreferencesDto, UserId userId)
        {
            var userPreferences = _userPreferencesRepository.GetUserPreferencesByUserId(userId);

            if (userPreferences is null)
                return UserPreferences.Create(userId.Value, userPreferencesDto.Theme, userPreferencesDto.Language)
                    .Map(newUserPreferences => _userPreferencesRepository.Add(newUserPreferences));

            userPreferences.Update(userPreferencesDto.Theme, userPreferencesDto.Language);
            return _userPreferencesRepository.Update(userPreferences);
        }
    }
}
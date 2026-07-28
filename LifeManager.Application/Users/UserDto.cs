namespace LifeManager.Application.Users
{
    public record UserDto(
        string Email,
        string Name,
        string UserPassword);
}
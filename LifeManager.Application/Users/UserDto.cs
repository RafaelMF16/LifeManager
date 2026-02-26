namespace LifeManager.Application.Users
{
    public record UserDto(
        int UserId,
        string Email,
        string Name,
        string UserPassword);
}
namespace LifeManager.Application.Users.DTOs
{
    public record UserDto(
        string Email,
        string Name,
        string UserPassword);
}
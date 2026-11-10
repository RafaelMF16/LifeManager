namespace LifeManager.Application.Users.DTOs
{
    public record CurrentUserResponseDto(string Name, string Email);

    public record UpdateCurrentUserDto(string Name);

    public record UpdatedUserNameResponseDto(string Name);
}
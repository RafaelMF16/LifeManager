namespace LifeManager.Application.Users.DTOs
{
    public record ChangePasswordDto(string CurrentPassword, string NewPassword);
}

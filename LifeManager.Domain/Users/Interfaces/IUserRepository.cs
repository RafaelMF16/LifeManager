namespace LifeManager.Domain.Users.Interfaces
{
    public interface IUserRepository
    {
        User Add(User user);
        User? GetUserByEmail(string email);
    }
}

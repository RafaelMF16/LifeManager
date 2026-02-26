namespace LifeManager.Domain.Users.Interfaces
{
    public interface IUserRepository
    {
        void Add(User user);
        User? GetUserByEmail(string email);
    }
}

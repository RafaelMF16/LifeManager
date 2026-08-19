using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Users
{
    public class UserRepository(LifeManagerDbContext dbContext) : IUserRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public User Add(User user)
        {
            _dbContext.Add(user);
            _dbContext.SaveChanges();

            return user;
        }

        public User? GetUserByEmail(string email)
        {
            var emailValueObject = Email.FromPersistence(email);

            var user = _dbContext.Users
                .AsNoTracking()
                .SingleOrDefault(user => user.Email == emailValueObject);

            return user;
        }
    }
}
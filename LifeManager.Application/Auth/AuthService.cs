namespace LifeManager.Application.Auth
{
    public class AuthService()
    {
        private const short WORK_FACTOR = 10;

        public string EncryptPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, WORK_FACTOR);
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }
}
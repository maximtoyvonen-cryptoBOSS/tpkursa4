using Npgsql;

namespace tpkursa4
{
    /// <summary>
    /// Сервис аутентификации. Вся работа с таблицей users живёт здесь,
    /// а не в LoginWindow.xaml.cs (бизнес-логика отделена от View).
    /// </summary>
    public class AuthService
    {
        private readonly string _connectionString;

        public AuthService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public static AuthService CreateDefault() =>
            new AuthService(DbRepository.CreateDefaultConnectionString());

        /// <summary>Возвращает true, если пользователь есть в public.users.</summary>
        public bool UserExists(string login)
        {
            if (string.IsNullOrWhiteSpace(login)) return false;

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(
                    "SELECT COUNT(1) FROM public.users WHERE username = @u", conn))
                {
                    cmd.Parameters.AddWithValue("u", login.Trim());
                    long count = (long)System.Convert.ToInt64(cmd.ExecuteScalar());
                    return count > 0;
                }
            }
        }
    }
}

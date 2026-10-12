using Npgsql;
using System;
using System.Security.Cryptography;
using System.Text;

namespace tpkursa4
{
    public class AuthService
    {
        private readonly string _connectionString;

        public AuthService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public static AuthService CreateDefault() =>
            new AuthService(DbRepository.CreateDefaultConnectionString());

        public UserAccount TryLogin(string login, string password)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
                return null;

            string hash = HashPassword(password);

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(@"
                    SELECT u.id, u.username, ur.role_name
                    FROM public.users u
                    JOIN public.user_roles ur ON u.role_id = ur.id
                    WHERE u.username = @u AND u.password_hash = @h", conn))
                {
                    cmd.Parameters.AddWithValue("u", login.Trim());
                    cmd.Parameters.AddWithValue("h", hash);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return null;
                        var user = new UserAccount(reader.GetInt32(0), reader.GetString(1), reader.GetString(2));
                        reader.Close();

                        using (var roleCmd = new NpgsqlCommand(
                            "SELECT 1 FROM public.get_users_by_role_name(@r) WHERE username = @u", conn))
                        {
                            roleCmd.Parameters.AddWithValue("r", user.RoleName);
                            roleCmd.Parameters.AddWithValue("u", user.Username);
                            if (roleCmd.ExecuteScalar() == null) return null;
                        }

                        return user;
                    }
                }
            }
        }

        public static string HashPassword(string password)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password ?? ""));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}

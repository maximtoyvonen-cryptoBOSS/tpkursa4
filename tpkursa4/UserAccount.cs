namespace tpkursa4
{
    public sealed class UserAccount
    {
        public int Id { get; }
        public string Username { get; }
        public string RoleName { get; }
        public bool IsAdmin => RoleName == "Администратор";

        public UserAccount(int id, string username, string roleName)
        {
            Id = id;
            Username = username ?? "";
            RoleName = roleName ?? "";
        }
    }
}

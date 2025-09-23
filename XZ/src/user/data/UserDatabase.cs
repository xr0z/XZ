using Newtonsoft.Json;
namespace XZ;

internal class User
{
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public bool IsAdmin { get; set; }
}

class UserDatabase
{
    private const string FilePath = "users.json";
    internal List<User> Users { get; set; } = new List<User>();

    internal void Load()
    {
        if (File.Exists(FilePath))
        {
            string json = File.ReadAllText(FilePath);
            Users = JsonConvert.DeserializeObject<List<User>>(json) ?? new List<User>();
        }
    }

    internal void Save()
    {
        string json = JsonConvert.SerializeObject(Users, Formatting.Indented);
        File.WriteAllText(FilePath, json);
    }

    internal void AddUser(string username, string password, bool isAdmin)
    {
        string hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        Users.Add(new User { Username = username, PasswordHash = hash, IsAdmin = isAdmin });
        Save();
    }

    internal bool VerifyUser(string username, string password)
    {
        var user = Users.Find(u => u.Username == username);
        if (user == null) return false;
        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }
    internal bool IsAdmin(string username)
    {
        var user = Users.Find(u => u.Username == username);
        return user?.IsAdmin ?? false;
    }

}
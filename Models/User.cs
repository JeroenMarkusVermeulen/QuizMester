// Model: één databasegebruiker (Id, Username, Role); IsAdmin is een handige berekende eigenschap.
namespace Quizmester.Models;

public sealed class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public bool IsAdmin => Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
}

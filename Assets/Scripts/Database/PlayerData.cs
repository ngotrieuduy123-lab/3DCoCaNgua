using System.Collections.Generic;

// Public profile only; password hashes and database types stay on the server.
public class PlayerData
{
    public string Id { get; set; }
    public string Username { get; set; }
    public string DisplayName { get; set; }
    public int Coins { get; set; }
    public List<string> OwnedSkinIds { get; set; } = new List<string>();
    public string EquippedSkinId { get; set; }
}

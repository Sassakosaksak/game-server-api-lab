namespace GameServerApi.Models;

public class PlayerInventoryItem
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string ItemCode { get; set; } = "";
    public DateTime AcquiredAt { get; set; }
}

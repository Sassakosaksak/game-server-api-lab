namespace GameServerApi.Models;

public class PurchaseHistory
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string ItemCode { get; set; } = "";
    public int PriceGold { get; set; }
    public DateTime PurchasedAt { get; set; }
}

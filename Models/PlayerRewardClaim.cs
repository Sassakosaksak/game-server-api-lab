namespace GameServerApi.Models;

public class PlayerRewardClaim
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string RewardCode { get; set; } = "";
    public DateOnly RewardDate { get; set; }
    public int GrantedGold { get; set; }
    public DateTime ClaimedAt { get; set; }
}

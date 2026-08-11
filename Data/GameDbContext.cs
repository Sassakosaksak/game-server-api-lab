using GameServerApi.Models;
using Microsoft.EntityFrameworkCore;

namespace GameServerApi.Data;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options)
        : base(options)
    {
    }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<PlayerRewardClaim> PlayerRewardClaims => Set<PlayerRewardClaim>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerRewardClaim>(entity =>
        {
            entity.Property(claim => claim.RewardCode).HasMaxLength(50);

            // 同じプレイヤーが同じ日の同じ報酬を二重に受け取れないようにする。
            entity.HasIndex(claim => new
            {
                claim.PlayerId,
                claim.RewardCode,
                claim.RewardDate
            }).IsUnique();

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(claim => claim.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

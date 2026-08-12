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
    public DbSet<PlayerInventoryItem> PlayerInventoryItems => Set<PlayerInventoryItem>();
    public DbSet<PurchaseHistory> PurchaseHistories => Set<PurchaseHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(entity =>
        {
            // ランキングのGold降順、Level降順、ID昇順の取得を支える複合インデックス。
            entity.HasIndex(player => new
            {
                player.Gold,
                player.Level,
                player.Id
            }).IsDescending(true, true, false);
        });

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

        modelBuilder.Entity<PlayerInventoryItem>(entity =>
        {
            entity.Property(item => item.ItemCode).HasMaxLength(50);
            entity.HasIndex(item => item.PlayerId);

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(item => item.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PurchaseHistory>(entity =>
        {
            entity.Property(history => history.ItemCode).HasMaxLength(50);
            entity.HasIndex(history => history.PlayerId);

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(history => history.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();
    public DbSet<WalletOpening> WalletOpenings => Set<WalletOpening>();
    public DbSet<FeloganRoom> FeloganRooms => Set<FeloganRoom>();
    public DbSet<FeloganCook> FeloganCooks => Set<FeloganCook>();
    public DbSet<FeloganMenuItem> FeloganMenuItems => Set<FeloganMenuItem>();
    public DbSet<FeloganServing> FeloganServings => Set<FeloganServing>();
    public DbSet<FeloganServingLine> FeloganServingLines => Set<FeloganServingLine>();
    public DbSet<FeloganPurchase> FeloganPurchases => Set<FeloganPurchase>();
    public DbSet<FeloganSettings> FeloganSettings => Set<FeloganSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<LedgerTransaction>(entity =>
        {
            entity.HasIndex(x => x.ClientRequestId).IsUnique();
            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => x.InChargeUserId);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Profit).HasPrecision(18, 2);
            entity.Property(x => x.DeltaCashOnHand).HasPrecision(18, 2);
            entity.Property(x => x.DeltaGcash).HasPrecision(18, 2);
            entity.Property(x => x.DeltaTpc).HasPrecision(18, 2);
            entity.Property(x => x.BalanceAfterCashOnHand).HasPrecision(18, 2);
            entity.Property(x => x.BalanceAfterGcash).HasPrecision(18, 2);
            entity.Property(x => x.BalanceAfterTpc).HasPrecision(18, 2);
            entity.Property(x => x.Method).HasMaxLength(64);
            entity.Property(x => x.Note).HasMaxLength(200);
        });

        builder.Entity<WalletOpening>(entity =>
        {
            entity.Property(x => x.CashOnHand).HasPrecision(18, 2);
            entity.Property(x => x.Gcash).HasPrecision(18, 2);
            entity.Property(x => x.Tpc).HasPrecision(18, 2);
        });

        builder.Entity<FeloganRoom>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(64);
        });

        builder.Entity<FeloganCook>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(64);
        });

        builder.Entity<FeloganMenuItem>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(64);
            entity.Property(x => x.Kind).HasMaxLength(16);
            entity.Property(x => x.SellPrice).HasPrecision(18, 2);
            entity.Property(x => x.Cost).HasPrecision(18, 2);
            entity.Property(x => x.OpeningPacks).HasDefaultValue(0);
        });

        builder.Entity<FeloganServing>(entity =>
        {
            entity.HasIndex(x => x.ClientRequestId).IsUnique();
            entity.HasIndex(x => x.ServedOn);
            entity.Property(x => x.ServedOn).HasColumnType("date");
            entity.Property(x => x.Note).HasMaxLength(200);
            entity.Property(x => x.AdditionalProfit).HasPrecision(18, 2);
            entity.Property(x => x.ExcessPaid).HasPrecision(18, 2);
            entity.Property(x => x.PaidCook).HasMaxLength(8);
            entity.Property(x => x.PaidSham).HasMaxLength(8);
            entity.Property(x => x.CookPercent).HasDefaultValue(40);
            entity.HasOne(x => x.Room)
                .WithMany()
                .HasForeignKey(x => x.RoomId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Cook)
                .WithMany()
                .HasForeignKey(x => x.CookId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FeloganServingLine>(entity =>
        {
            entity.Property(x => x.SnapshotName).HasMaxLength(64);
            entity.Property(x => x.SnapshotKind).HasMaxLength(16);
            entity.Property(x => x.SnapshotSellPrice).HasPrecision(18, 2);
            entity.Property(x => x.SnapshotCost).HasPrecision(18, 2);
            entity.HasOne(x => x.Serving)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.ServingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MenuItem)
                .WithMany()
                .HasForeignKey(x => x.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FeloganPurchase>(entity =>
        {
            entity.HasIndex(x => x.ClientRequestId).IsUnique();
            entity.HasIndex(x => x.PurchasedOn);
            entity.Property(x => x.PurchasedOn).HasColumnType("date");
            entity.Property(x => x.Note).HasMaxLength(200);
            entity.HasOne(x => x.MenuItem)
                .WithMany()
                .HasForeignKey(x => x.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<FeloganSettings>(entity =>
        {
            entity.Property(x => x.CookPercent).HasDefaultValue(40);
        });
    }
}

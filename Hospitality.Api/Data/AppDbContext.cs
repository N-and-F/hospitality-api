using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();
    public DbSet<WalletOpening> WalletOpenings => Set<WalletOpening>();

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
    }
}

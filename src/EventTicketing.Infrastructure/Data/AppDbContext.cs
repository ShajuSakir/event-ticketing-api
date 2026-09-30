using EventTicketing.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Infrastructure.Data;



public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<PricingTier> PricingTiers => Set<PricingTier>();
    public DbSet<TicketOrder> TicketOrders => Set<TicketOrder>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Venue).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);

            e.HasMany(x => x.PricingTiers)
                .WithOne(t => t.Event)
                .HasForeignKey(t => t.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.TicketOrders)
                .WithOne(o => o.Event)
                .HasForeignKey(o => o.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PricingTier>(t =>
        {
            t.HasKey(x => x.Id);
            t.Property(x => x.Name).IsRequired().HasMaxLength(100);
            t.Property(x => x.Price).HasPrecision(10, 2);
            t.Property(x => x.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<TicketOrder>(o =>
        {
            o.HasKey(x => x.Id);
            o.Property(x => x.CustomerName).IsRequired().HasMaxLength(200);
            o.Property(x => x.CustomerEmail).IsRequired().HasMaxLength(200);
            o.Property(x => x.UnitPrice).HasPrecision(10, 2);
            o.Property(x => x.TotalPrice).HasPrecision(10, 2);
            o.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");

            o.HasOne(x => x.PricingTier)
                .WithMany()
                .HasForeignKey(x => x.PricingTierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(u =>
        {
            u.HasKey(x => x.Id);
            u.Property(x => x.Username).IsRequired().HasMaxLength(100);
            u.Property(x => x.Email).IsRequired().HasMaxLength(200);
            u.Property(x => x.Role).IsRequired().HasMaxLength(50);
            u.HasIndex(x => x.Username).IsUnique();
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BumpConcurrencyTokens();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        BumpConcurrencyTokens();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Updates the concurrency version for modified PricingTiers to detect conflicting updates.
    private void BumpConcurrencyTokens()
    {
        foreach (var entry in ChangeTracker.Entries<PricingTier>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Property(x => x.Version).OriginalValue = entry.Entity.Version;
                entry.Entity.Version++;
            }
        }
    }
}

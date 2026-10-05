using BookingsApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookingsApi.Data;

/// <summary>
/// Source of truth for the schema (EF migrations in Data/Migrations; docs/schema.sql is reference only).
/// The slot no-overlap exclusion constraint (EX_Slots_NoOverlap, SqlState 23P01) cannot be modelled
/// by EF and is added with raw SQL in the InitialCreate migration.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Slot> Slots => Set<Slot>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("btree_gist");

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "\"Role\" IN ('Customer','Provider')"));
            e.HasKey(x => x.Id).HasName("PK_Users");
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UX_Users_Email");
        });

        modelBuilder.Entity<Resource>(e =>
        {
            e.ToTable("Resources");
            e.HasKey(x => x.Id).HasName("PK_Resources");
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
            e.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Resources_Users_ProviderId");
            e.HasIndex(x => x.ProviderId).HasDatabaseName("IX_Resources_ProviderId");
        });

        modelBuilder.Entity<Slot>(e =>
        {
            e.ToTable("Slots", t =>
            {
                t.HasCheckConstraint("CK_Slots_EndAfterStart", "\"EndUtc\" > \"StartUtc\"");
                t.HasCheckConstraint("CK_Slots_Price", "\"PriceCents\" >= 0");
            });
            e.HasKey(x => x.Id).HasName("PK_Slots");
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.StartUtc).HasColumnType("timestamptz");
            e.Property(x => x.EndUtc).HasColumnType("timestamptz");
            e.HasOne(x => x.Resource).WithMany(r => r.Slots).HasForeignKey(x => x.ResourceId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Slots_Resources_ResourceId");
            e.HasIndex(x => new { x.ResourceId, x.StartUtc }).HasDatabaseName("IX_Slots_ResourceId_StartUtc");
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.ToTable("Bookings", t =>
            {
                t.HasCheckConstraint("CK_Bookings_Status", "\"Status\" IN ('Pending','Confirmed','PaymentFailed','Cancelled')");
                t.HasCheckConstraint("CK_Bookings_Amount", "\"AmountCents\" >= 0");
            });
            e.HasKey(x => x.Id).HasName("PK_Bookings");
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            e.Property(x => x.PaymentRef).HasMaxLength(100);
            e.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
            e.HasOne(x => x.Slot).WithMany().HasForeignKey(x => x.SlotId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Bookings_Slots_SlotId");
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Bookings_Users_CustomerId");
            e.HasIndex(x => x.SlotId).IsUnique()
                .HasFilter("\"Status\" IN ('Pending','Confirmed')")
                .HasDatabaseName("UX_Bookings_SlotId_Active");
            e.HasIndex(x => new { x.CustomerId, x.IdempotencyKey }).IsUnique()
                .HasDatabaseName("UX_Bookings_CustomerId_IdempotencyKey");
            e.HasIndex(x => new { x.CustomerId, x.CreatedAt }).IsDescending(false, true)
                .HasDatabaseName("IX_Bookings_CustomerId_CreatedAt");
        });
    }
}

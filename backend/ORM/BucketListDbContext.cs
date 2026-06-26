using BucketListAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BucketListAPI.ORM;

public class BucketListDbContext : DbContext
{
    public BucketListDbContext(DbContextOptions<BucketListDbContext> options)
        : base(options)
    {
    }

    public DbSet<TravelPlace> TravelPlaces => Set<TravelPlace>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Bikepark> Bikeparks => Set<Bikepark>();

    public DbSet<Trail> Trails => Set<Trail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TravelPlace>(entity =>
        {
            entity.HasKey(place => place.Id);
            entity.Property(place => place.Name).HasMaxLength(120).IsRequired();
            entity.Property(place => place.Country).HasMaxLength(120);
            entity.Property(place => place.Region).HasMaxLength(120);
            entity.Property(place => place.Notes).HasMaxLength(1000);
            entity.Property(place => place.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(place => place.PhotoUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasMaxLength(255).IsRequired();
            entity.Property(user => user.PasswordHash).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<Bikepark>(entity =>
        {
            entity.HasKey(park => park.Id);
            entity.Property(park => park.Name).HasMaxLength(120).IsRequired();
            entity.Property(park => park.Country).HasMaxLength(120);
            entity.Property(park => park.Region).HasMaxLength(120);
            entity.Property(park => park.Description).HasMaxLength(2000);
            entity.Property(park => park.Difficulty).HasMaxLength(32);
            entity.Property(park => park.Website).HasMaxLength(500);
            entity.Property(park => park.PhotoUrl).HasMaxLength(500);
            entity.HasMany(park => park.Trails)
                .WithOne(trail => trail.Bikepark)
                .HasForeignKey(trail => trail.BikeparkId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Trail>(entity =>
        {
            entity.HasKey(trail => trail.Id);
            entity.Property(trail => trail.Name).HasMaxLength(120).IsRequired();
            entity.Property(trail => trail.Difficulty).HasMaxLength(32);
            entity.Property(trail => trail.TrailType).HasMaxLength(64);
            entity.Property(trail => trail.Description).HasMaxLength(2000);
            entity.Property(trail => trail.Polyline).HasMaxLength(8000);
        });
    }
}
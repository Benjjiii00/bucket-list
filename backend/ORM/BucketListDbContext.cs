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
        });
    }
}
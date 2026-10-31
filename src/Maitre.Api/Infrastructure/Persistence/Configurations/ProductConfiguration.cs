using Maitre.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maitre.Api.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("Products", table => table.HasCheckConstraint("CK_Products_Price", "\"Price\" >= 0"));
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.IsAvailable).HasDefaultValue(true);
        builder.HasIndex(x => x.RestaurantId);
        builder.HasOne(x => x.Restaurant).WithMany(x => x.Products)
            .HasForeignKey(x => x.RestaurantId).OnDelete(DeleteBehavior.Restrict);
    }
}


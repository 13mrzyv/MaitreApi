using Maitre.Api.Domain.Entities;
using Maitre.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maitre.Api.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("Orders", table => table.HasCheckConstraint("CK_Orders_Status",
            "\"Status\" IN ('Pending', 'Preparing', 'Ready', 'Completed', 'Cancelled')"));
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20)
            .IsRequired().HasDefaultValue(OrderStatus.Pending);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Ignore(x => x.TotalAmount);
        builder.HasIndex(x => x.RestaurantId);
        builder.HasOne(x => x.Restaurant).WithMany(x => x.Orders)
            .HasForeignKey(x => x.RestaurantId).OnDelete(DeleteBehavior.Restrict);
    }
}


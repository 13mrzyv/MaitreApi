using Maitre.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maitre.Api.Infrastructure.Seed;

internal static class RestaurantSeed
{
    public static void Configure(EntityTypeBuilder<Restaurant> builder) => builder.HasData(
        new Restaurant { Id = 1, Name = "Maitre Central" },
        new Restaurant { Id = 2, Name = "Maitre Garden" },
        new Restaurant { Id = 3, Name = "Maitre Express" });
}


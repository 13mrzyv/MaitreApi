using Maitre.Api.Domain.Enums;
using Maitre.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Maitre.Api.Features.Reports.GetTopSellingProducts;

public static class GetTopSellingProductsEndpoint
{
    public static RouteGroupBuilder MapGetTopSellingProducts(this RouteGroupBuilder group)
    {
        group.MapGet("/top-selling-products", HandleAsync)
            .WithName("GetTopSellingProducts")
            .Produces<GetTopSellingProductsResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        return group;
    }

    private static async Task<Results<Ok<GetTopSellingProductsResponse>, ValidationProblem, ProblemHttpResult>>
        HandleAsync(int restaurantId, AppDbContext db, CancellationToken cancellationToken)
    {
        if (restaurantId <= 0)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["RestaurantId"] = ["RestaurantId must be greater than zero."]
            });

        if (!await db.Restaurants.AnyAsync(x => x.Id == restaurantId, cancellationToken))
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Restaurant not found.");

        var products = await db.OrderItems.AsNoTracking()
            .Where(item => item.Order.RestaurantId == restaurantId
                && item.Order.Status != OrderStatus.Cancelled)
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                ProductName = group.Min(item => EF.Functions.Collate(item.ProductName, "C"))!,
                TotalQuantity = group.Sum(item => item.Quantity),
                TotalRevenue = group.Sum(item => item.Quantity * item.UnitPrice)
            })
            .OrderByDescending(product => product.TotalQuantity)
            .ThenBy(product => product.ProductId)
            .Take(5)
            .Select(product => new TopSellingProductResponse(product.ProductId,
                product.ProductName, product.TotalQuantity, product.TotalRevenue))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new GetTopSellingProductsResponse(restaurantId, products));
    }
}

public sealed record GetTopSellingProductsResponse(
    int RestaurantId, List<TopSellingProductResponse> Products);

public sealed record TopSellingProductResponse(
    int ProductId, string ProductName, int TotalQuantity, decimal TotalRevenue);

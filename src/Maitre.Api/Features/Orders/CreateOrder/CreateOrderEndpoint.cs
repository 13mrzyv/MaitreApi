using System.Text.Json.Serialization;
using Maitre.Api.Domain.Entities;
using Maitre.Api.Domain.Enums;
using Maitre.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Maitre.Api.Features.Orders.CreateOrder;

public static class CreateOrderEndpoint
{
    public static RouteGroupBuilder MapCreateOrder(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync)
            .WithName("CreateOrder")
            .Produces<CreateOrderResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        return group;
    }

    private static async Task<Results<Created<CreateOrderResponse>, ValidationProblem, ProblemHttpResult>>
        HandleAsync(int restaurantId, CreateOrderRequest request, AppDbContext db,
            CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        if (!await db.Restaurants.AnyAsync(x => x.Id == restaurantId, cancellationToken))
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Restaurant not found.");

        var requestedItems = request.Items!;
        var productIds = requestedItems.Select(x => x!.ProductId).ToArray();
        var products = await db.Products.AsNoTracking()
            .Where(x => x.RestaurantId == restaurantId && productIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, x.Price, x.IsAvailable })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        for (var index = 0; index < requestedItems.Count; index++)
        {
            var item = requestedItems[index]!;
            if (!products.TryGetValue(item.ProductId, out var product))
                errors[$"Items[{index}].ProductId"] = ["Product does not exist in this restaurant."];
            else if (!product.IsAvailable)
                errors[$"Items[{index}].ProductId"] = ["Product is not available."];
        }

        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var order = new Order
        {
            RestaurantId = restaurantId,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Items = requestedItems.Select(item => new OrderItem
            {
                ProductId = item!.ProductId,
                ProductName = products[item.ProductId].Name,
                UnitPrice = products[item.ProductId].Price,
                Quantity = item.Quantity
            }).ToList()
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/api/restaurants/{restaurantId}/orders/{order.Id}",
            new CreateOrderResponse(order.Id, order.RestaurantId, order.Status.ToString(),
                order.CreatedAt, order.Items.Select(item => new CreateOrderItemResponse(
                    item.ProductId, item.ProductName, item.Quantity, item.UnitPrice,
                    item.LineTotal)).ToArray(), order.TotalAmount));
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateOrderRequest(IReadOnlyList<CreateOrderItemRequest?>? Items)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (Items is null || Items.Count == 0)
        {
            errors[nameof(Items)] = ["At least one order item is required."];
            return errors;
        }

        var productIds = new HashSet<int>();
        for (var index = 0; index < Items.Count; index++)
        {
            var item = Items[index];
            if (item is null)
            {
                errors[$"Items[{index}]"] = ["Order item must not be null."];
                continue;
            }

            if (item.ProductId <= 0)
                errors[$"Items[{index}].ProductId"] = ["ProductId must be greater than zero."];
            else if (!productIds.Add(item.ProductId))
                errors[$"Items[{index}].ProductId"] = ["Duplicate ProductId is not allowed."];

            if (item.Quantity <= 0)
                errors[$"Items[{index}].Quantity"] = ["Quantity must be greater than zero."];
        }

        return errors;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateOrderItemRequest(int ProductId, int Quantity);

public sealed record CreateOrderResponse(
    int Id, int RestaurantId, string Status, DateTime CreatedAt,
    IReadOnlyList<CreateOrderItemResponse> Items, decimal TotalAmount);

public sealed record CreateOrderItemResponse(
    int ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);


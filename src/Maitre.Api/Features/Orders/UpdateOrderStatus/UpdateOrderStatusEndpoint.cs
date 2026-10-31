using System.Text.Json.Serialization;
using Maitre.Api.Domain.Enums;
using Maitre.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Maitre.Api.Features.Orders.UpdateOrderStatus;

public static class UpdateOrderStatusEndpoint
{
    public static RouteGroupBuilder MapUpdateOrderStatus(this RouteGroupBuilder group)
    {
        group.MapPatch("/{orderId:int}/status", HandleAsync)
            .WithName("UpdateOrderStatus")
            .Produces<UpdateOrderStatusResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        return group;
    }

    private static async Task<Results<Ok<UpdateOrderStatusResponse>, ValidationProblem, ProblemHttpResult>>
        HandleAsync(int restaurantId, int orderId, UpdateOrderStatusRequest request,
            AppDbContext db, CancellationToken cancellationToken)
    {
        var errors = request.Validate(out var nextStatus);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        if (!await db.Restaurants.AnyAsync(x => x.Id == restaurantId, cancellationToken))
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Restaurant not found.");

        var order = await db.Orders.AsNoTracking()
            .Where(x => x.Id == orderId && x.RestaurantId == restaurantId)
            .Select(x => new { x.Id, x.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null)
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Order not found.");

        if (!CanTransition(order.Status, nextStatus))
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Invalid status transition.",
                detail: $"Cannot change order status from {order.Status} to {nextStatus}.");

        var affectedRows = await db.Orders
            .Where(x => x.Id == orderId && x.RestaurantId == restaurantId && x.Status == order.Status)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, nextStatus),
                cancellationToken);
        if (affectedRows == 0)
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Order was changed concurrently.",
                detail: "Reload the order before retrying.");

        return TypedResults.Ok(new UpdateOrderStatusResponse(order.Id, restaurantId,
            nextStatus.ToString()));
    }

    private static bool CanTransition(OrderStatus current, OrderStatus next) => (current, next) switch
    {
        (OrderStatus.Pending, OrderStatus.Preparing or OrderStatus.Cancelled) => true,
        (OrderStatus.Preparing, OrderStatus.Ready) => true,
        (OrderStatus.Ready, OrderStatus.Completed) => true,
        _ => false
    };
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateOrderStatusRequest(string? Status)
{
    public Dictionary<string, string[]> Validate(out OrderStatus status)
    {
        var errors = new Dictionary<string, string[]>();
        status = default;
        if (Status is null || !Enum.GetNames<OrderStatus>().Contains(Status)
            || !Enum.TryParse(Status, out status))
            errors[nameof(Status)] = ["Status must be Pending, Preparing, Ready, Completed or Cancelled."];
        return errors;
    }
}

public sealed record UpdateOrderStatusResponse(int Id, int RestaurantId, string Status);


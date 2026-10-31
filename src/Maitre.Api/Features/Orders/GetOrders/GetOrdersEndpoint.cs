using Maitre.Api.Domain.Enums;
using Maitre.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Maitre.Api.Features.Orders.GetOrders;

public static class GetOrdersEndpoint
{
    public static RouteGroupBuilder MapGetOrders(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync)
            .WithName("GetOrders")
            .Produces<GetOrdersResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        return group;
    }

    private static async Task<Results<Ok<GetOrdersResponse>, ValidationProblem, ProblemHttpResult>>
        HandleAsync(int restaurantId, [AsParameters] GetOrdersRequest request, AppDbContext db,
            CancellationToken cancellationToken)
    {
        var errors = request.Validate(out var status);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        if (!await db.Restaurants.AnyAsync(x => x.Id == restaurantId, cancellationToken))
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Restaurant not found.");

        var query = db.Orders.AsNoTracking().Where(x => x.RestaurantId == restaurantId);
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(order => new GetOrdersOrderResponse(
                order.Id, order.RestaurantId, order.Status.ToString(), order.CreatedAt,
                order.Items.OrderBy(item => item.Id).Select(item => new GetOrdersItemResponse(
                    item.ProductId, item.ProductName, item.Quantity, item.UnitPrice,
                    item.Quantity * item.UnitPrice)).ToList(),
                order.Items.Sum(item => item.Quantity * item.UnitPrice)))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new GetOrdersResponse(request.Page, request.PageSize, totalCount,
            (int)Math.Ceiling((double)totalCount / request.PageSize), items));
    }
}

public sealed record GetOrdersRequest(string? Status = null, int Page = 1, int PageSize = 10)
{
    public Dictionary<string, string[]> Validate(out OrderStatus? status)
    {
        var errors = new Dictionary<string, string[]>();
        status = null;
        if (Status is not null)
        {
            if (Enum.GetNames<OrderStatus>().Contains(Status)
                && Enum.TryParse<OrderStatus>(Status, out var parsed))
                status = parsed;
            else
                errors[nameof(Status)] = ["Status must be Pending, Preparing, Ready, Completed or Cancelled."];
        }

        if (Page < 1)
            errors[nameof(Page)] = ["Page must be at least 1."];
        if (PageSize is < 1 or > 100)
            errors[nameof(PageSize)] = ["PageSize must be between 1 and 100."];
        if (Page >= 1 && PageSize is >= 1 and <= 100 && (long)(Page - 1) * PageSize > int.MaxValue)
            errors[nameof(Page)] = ["The requested page offset is too large."];
        return errors;
    }
}

public sealed record GetOrdersResponse(
    int Page, int PageSize, int TotalCount, int TotalPages, IReadOnlyList<GetOrdersOrderResponse> Items);

public sealed record GetOrdersOrderResponse(
    int Id, int RestaurantId, string Status, DateTime CreatedAt,
    IReadOnlyList<GetOrdersItemResponse> Items, decimal TotalAmount);

public sealed record GetOrdersItemResponse(
    int ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);


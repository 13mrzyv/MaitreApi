using Maitre.Api.Features.Orders.CreateOrder;
using Maitre.Api.Features.Orders.GetOrders;
using Maitre.Api.Features.Orders.UpdateOrderStatus;

namespace Maitre.Api.Features.Orders;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/restaurants/{restaurantId:int}/orders")
            .WithTags("Orders");
        group.MapCreateOrder();
        group.MapUpdateOrderStatus();
        group.MapGetOrders();
        return endpoints;
    }
}


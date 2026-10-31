using Maitre.Api.Features.Products.CreateProduct;
using Maitre.Api.Features.Products.UpdateProduct;

namespace Maitre.Api.Features.Products;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/restaurants/{restaurantId:int}/products")
            .WithTags("Products");
        group.MapCreateProduct();
        group.MapUpdateProduct();
        return endpoints;
    }
}

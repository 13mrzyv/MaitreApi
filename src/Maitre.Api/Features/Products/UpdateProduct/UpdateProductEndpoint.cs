using System.Text.Json.Serialization;
using Maitre.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Maitre.Api.Features.Products.UpdateProduct;

public static class UpdateProductEndpoint
{
    public static RouteGroupBuilder MapUpdateProduct(this RouteGroupBuilder group)
    {
        group.MapPatch("/{productId:int}", HandleAsync)
            .WithName("UpdateProduct")
            .Produces<UpdateProductResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        return group;
    }

    private static async Task<Results<Ok<UpdateProductResponse>, ValidationProblem, ProblemHttpResult>>
        HandleAsync(int restaurantId, int productId, UpdateProductRequest request, AppDbContext db,
            CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        if (!await db.Restaurants.AnyAsync(x => x.Id == restaurantId, cancellationToken))
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Restaurant not found.");

        var product = await db.Products.SingleOrDefaultAsync(
            x => x.RestaurantId == restaurantId && x.Id == productId, cancellationToken);
        if (product is null)
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Product not found.");

        if (request.HasPrice)
            product.Price = request.Price!.Value;
        if (request.HasIsAvailable)
            product.IsAvailable = request.IsAvailable!.Value;

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new UpdateProductResponse(product.Id, product.RestaurantId,
            product.Name, product.Category, product.Price, product.IsAvailable));
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateProductRequest
{
    private decimal? price;
    private bool? isAvailable;

    public decimal? Price
    {
        get => price;
        set { price = value; HasPrice = true; }
    }

    public bool? IsAvailable
    {
        get => isAvailable;
        set { isAvailable = value; HasIsAvailable = true; }
    }

    [JsonIgnore]
    public bool HasPrice { get; private set; }

    [JsonIgnore]
    public bool HasIsAvailable { get; private set; }

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (!HasPrice && !HasIsAvailable)
            errors["Request"] = ["At least one of Price or IsAvailable must be provided."];

        if (HasPrice)
        {
            if (Price is null)
                errors[nameof(Price)] = ["Price must not be null."];
            else
                ValidatePrice(Price.Value, errors);
        }

        if (HasIsAvailable && IsAvailable is null)
            errors[nameof(IsAvailable)] = ["IsAvailable must not be null."];

        return errors;
    }

    private static void ValidatePrice(decimal price, Dictionary<string, string[]> errors)
    {
        if (price <= 0)
            errors["Price"] = ["Price must be greater than zero."];
        else if (price > 9999999999999999.99m)
            errors["Price"] = ["Price exceeds the supported maximum of 9999999999999999.99."];
        else if (decimal.Round(price, 2) != price)
            errors["Price"] = ["Price must have at most two decimal places."];
    }
}

public sealed record UpdateProductResponse(
    int Id,
    int RestaurantId,
    string Name,
    string Category,
    decimal Price,
    bool IsAvailable);

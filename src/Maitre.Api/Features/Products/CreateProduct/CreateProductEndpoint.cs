using System.Text.Json.Serialization;
using Maitre.Api.Domain.Entities;
using Maitre.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Maitre.Api.Features.Products.CreateProduct;

public static class CreateProductEndpoint
{
    public static RouteGroupBuilder MapCreateProduct(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync)
            .WithName("CreateProduct")
            .Produces<CreateProductResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        return group;
    }

    private static async Task<Results<Created<CreateProductResponse>, ValidationProblem, ProblemHttpResult>>
        HandleAsync(int restaurantId, CreateProductRequest request, AppDbContext db,
            CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        if (!await db.Restaurants.AnyAsync(x => x.Id == restaurantId, cancellationToken))
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Restaurant not found.");

        var product = new Product
        {
            RestaurantId = restaurantId,
            Name = request.Name!.Trim(),
            Category = request.Category!.Trim(),
            Price = request.Price,
            IsAvailable = true
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/api/restaurants/{restaurantId}/products/{product.Id}",
            new CreateProductResponse(product.Id, product.RestaurantId, product.Name,
                product.Category, product.Price, product.IsAvailable));
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateProductRequest(string? Name, string? Category, decimal Price)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        ValidateText(Name, nameof(Name), 200, errors);
        ValidateText(Category, nameof(Category), 100, errors);
        ValidatePrice(Price, errors);
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

    private static void ValidateText(string? value, string field, int maxLength,
        Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors[field] = [$"{field} is required."];
        else if (value.Contains('\0'))
            errors[field] = [$"{field} must not contain null characters."];
        else if (value.Trim().Length > maxLength)
            errors[field] = [$"{field} must not exceed {maxLength} characters."];
    }
}

public sealed record CreateProductResponse(
    int Id,
    int RestaurantId,
    string Name,
    string Category,
    decimal Price,
    bool IsAvailable);

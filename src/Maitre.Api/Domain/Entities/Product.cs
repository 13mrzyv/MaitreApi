namespace Maitre.Api.Domain.Entities;

public sealed class Product
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    public Restaurant Restaurant { get; set; } = null!;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}


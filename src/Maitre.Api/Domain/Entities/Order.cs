using Maitre.Api.Domain.Enums;

namespace Maitre.Api.Domain.Entities;

public sealed class Order
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Restaurant Restaurant { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public decimal TotalAmount => Items.Sum(item => item.LineTotal);
}


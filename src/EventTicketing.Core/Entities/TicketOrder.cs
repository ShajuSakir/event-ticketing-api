namespace EventTicketing.Core.Entities;

public class TicketOrder
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Event? Event { get; set; }

    public Guid PricingTierId { get; set; }
    public PricingTier? PricingTier { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime PurchasedAtUtc { get; set; }
}

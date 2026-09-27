using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Core.Dtos;

public class PurchaseTicketRequest
{
    [Required]
    public Guid PricingTierId { get; set; }

    [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100.")]
    public int Quantity { get; set; }
}

public class TicketOrderResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid PricingTierId { get; set; }
    public string PricingTierName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime PurchasedAtUtc { get; set; }
}

public class TicketAvailabilityResponse
{
    public Guid EventId { get; set; }
    public int TotalCapacity { get; set; }
    public int TotalSold { get; set; }
    public int TotalRemaining { get; set; }
    public List<PricingTierResponse> Tiers { get; set; } = new();
}

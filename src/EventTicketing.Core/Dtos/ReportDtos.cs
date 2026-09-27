namespace EventTicketing.Core.Dtos;

public class TierSalesSummary
{
    public Guid PricingTierId { get; set; }
    public string TierName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int TicketsSold { get; set; }
    public int Remaining { get; set; }
    public decimal Revenue { get; set; }
}

public class EventSalesSummary
{
    public Guid EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public int TotalCapacity { get; set; }
    public int TotalTicketsSold { get; set; }
    public int TotalRemaining { get; set; }
    public decimal TotalRevenue { get; set; }
    public int OrderCount { get; set; }
    public List<TierSalesSummary> TierBreakdown { get; set; } = new();
}

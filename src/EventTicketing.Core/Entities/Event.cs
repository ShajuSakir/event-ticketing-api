namespace EventTicketing.Core.Entities;

public class Event
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public int TotalCapacity { get; set; }

    public List<PricingTier> PricingTiers { get; set; } = new();
    public List<TicketOrder> TicketOrders { get; set; } = new();
}

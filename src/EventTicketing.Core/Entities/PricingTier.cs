namespace EventTicketing.Core.Entities;

public class PricingTier
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Event? Event { get; set; }

    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Capacity { get; set; }
    public int TicketsSold { get; set; }

    // app managed concurrency token, prevents lost updates when two purchases race for the last tickets.
    public uint Version { get; set; }

    public int Remaining => Capacity - TicketsSold;
}

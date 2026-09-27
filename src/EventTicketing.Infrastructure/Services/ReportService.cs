using EventTicketing.Core.Dtos;
using EventTicketing.Core.Entities;
using EventTicketing.Core.Interfaces;
using EventTicketing.Core.Results;
using EventTicketing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    // sales summary (tickets sold, revenue, remaining, per-tier breakdown) for one event
    public async Task<ServiceResult<EventSalesSummary>> GetEventSalesSummaryAsync(
    Guid eventId,
    CancellationToken ct = default)
    {
        var evt = await _db.Events
            .Include(e => e.PricingTiers)
            .Include(e => e.TicketOrders)
            .FirstOrDefaultAsync(e => e.Id == eventId, ct);

        if (evt is null)
        {
            return ServiceResult<EventSalesSummary>.Failure(
                ServiceErrorType.NotFound,
                $"Event '{eventId}' was not found.");
        }

        return ServiceResult<EventSalesSummary>.Success(BuildSummary(evt));
    }

    // sales summary for every event
    public async Task<List<EventSalesSummary>> GetAllEventsSalesSummaryAsync(CancellationToken ct = default)
    {
        var events = await _db.Events
            .Include(e => e.PricingTiers)
            .Include(e => e.TicketOrders)
            .OrderBy(e => e.Date).ThenBy(e => e.Time)
            .ToListAsync(ct);

        return events.Select(BuildSummary).ToList();
    }

    // event sales summary by pricing tier.
    private static EventSalesSummary BuildSummary(Event evt)
    {
        var tierBreakdown = evt.PricingTiers.Select(t => new TierSalesSummary
        {
            PricingTierId = t.Id,
            TierName = t.Name,
            Capacity = t.Capacity,
            TicketsSold = t.TicketsSold,
            Remaining = t.Remaining,
            Revenue = evt.TicketOrders.Where(o => o.PricingTierId == t.Id).Sum(o => o.TotalPrice)
        }).ToList();

        return new EventSalesSummary
        {
            EventId = evt.Id,
            EventName = evt.Name,
            TotalCapacity = evt.TotalCapacity,
            TotalTicketsSold = tierBreakdown.Sum(t => t.TicketsSold),
            TotalRemaining = tierBreakdown.Sum(t => t.Remaining),
            TotalRevenue = tierBreakdown.Sum(t => t.Revenue),
            OrderCount = evt.TicketOrders.Count,
            TierBreakdown = tierBreakdown
        };
    }
}

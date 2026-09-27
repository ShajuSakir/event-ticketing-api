using EventTicketing.Core.Dtos;
using EventTicketing.Core.Entities;
using EventTicketing.Core.Interfaces;
using EventTicketing.Core.Results;
using EventTicketing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Infrastructure.Services;

public class EventService : IEventService
{
    private readonly AppDbContext _db;

    public EventService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<EventResponse>> CreateAsync(
        CreateEventRequest request,
        CancellationToken ct = default)
    {
        var totalTierCapacity = request.PricingTiers.Sum(t => t.Capacity);

        if (totalTierCapacity != request.TotalCapacity)
        {
            return ServiceResult<EventResponse>.Failure(
                ServiceErrorType.Validation,
                "The total capacity of pricing tiers must equal the event total capacity.");
        }

        var evt = new Event
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Venue = request.Venue,
            Date = request.Date,
            Time = request.Time,
            TotalCapacity = request.TotalCapacity,
            PricingTiers = request.PricingTiers.Select(t => new PricingTier
            {
                Id = Guid.NewGuid(),
                Name = t.Name,
                Price = t.Price,
                Capacity = t.Capacity,
                TicketsSold = 0
            }).ToList()
        };

        _db.Events.Add(evt);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<EventResponse>.Success(ToResponse(evt));
    }

    public async Task<ServiceResult<EventResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var evt = await _db.Events.Include(e => e.PricingTiers)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        return evt is null
            ? ServiceResult<EventResponse>.Failure(ServiceErrorType.NotFound, $"Event '{id}' was not found.")
            : ServiceResult<EventResponse>.Success(ToResponse(evt));
    }

    public async Task<List<EventResponse>> GetAllAsync(
     int page,
     int pageSize,
     CancellationToken ct = default)
    {
        var events = await _db.Events
            .Include(e => e.PricingTiers)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Time)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return events.Select(ToResponse).ToList();
    }

    public async Task<ServiceResult<EventResponse>> UpdateAsync(Guid id, UpdateEventRequest request, CancellationToken ct = default)
    {
        var evt = await _db.Events.Include(e => e.PricingTiers)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (evt is null)
            return ServiceResult<EventResponse>.Failure(ServiceErrorType.NotFound, $"Event '{id}' was not found.");

        // Keep event capacity consistent with the existing pricing tiers.
        var tierCapacity = evt.PricingTiers.Sum(t => t.Capacity);

        if (request.TotalCapacity != tierCapacity)
        {
            return ServiceResult<EventResponse>.Failure(
                ServiceErrorType.Validation,
                $"TotalCapacity must equal the total capacity of the existing pricing tiers ({tierCapacity}).");
        }

        evt.Name = request.Name;
        evt.Description = request.Description;
        evt.Venue = request.Venue;
        evt.Date = request.Date;
        evt.Time = request.Time;
        evt.TotalCapacity = request.TotalCapacity;

        await _db.SaveChangesAsync(ct);
        return ServiceResult<EventResponse>.Success(ToResponse(evt));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var evt = await _db.Events.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (evt is null)
            return ServiceResult<bool>.Failure(ServiceErrorType.NotFound, $"Event '{id}' was not found.");

        var hasSales = await _db.TicketOrders.AnyAsync(o => o.EventId == id, ct);
        if (hasSales)
        {
            return ServiceResult<bool>.Failure(
                ServiceErrorType.Conflict,
                "Cannot delete an event that already has ticket sales.");
        }

        _db.Events.Remove(evt);
        await _db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    // maps ef entity to response dto
    private static EventResponse ToResponse(Event evt) => new()
    {
        Id = evt.Id,
        Name = evt.Name,
        Description = evt.Description,
        Venue = evt.Venue,
        Date = evt.Date,
        Time = evt.Time,
        TotalCapacity = evt.TotalCapacity,
        PricingTiers = evt.PricingTiers.Select(t => new PricingTierResponse
        {
            Id = t.Id,
            Name = t.Name,
            Price = t.Price,
            Capacity = t.Capacity,
            TicketsSold = t.TicketsSold,
            Remaining = t.Remaining
        }).ToList()
    };
}

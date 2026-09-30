using EventTicketing.Core.Dtos;
using EventTicketing.Core.Entities;
using EventTicketing.Core.Interfaces;
using EventTicketing.Core.Results;
using EventTicketing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Infrastructure.Services;

public class TicketService : ITicketService
{
    private const int MaxConcurrencyRetries = 5;

    private readonly AppDbContext _db;

    public TicketService(AppDbContext db)
    {
        _db = db;
    }

    // Purchases tickets while preventing overselling using optimistic concurrency and retry handling.
    public async Task<ServiceResult<TicketOrderResponse>> PurchaseAsync(Guid eventId, Guid userId, PurchaseTicketRequest request, string? idempotencyKey = null, CancellationToken ct = default)
    {

        var evt = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);

        if (evt is null)
        {
            return ServiceResult<TicketOrderResponse>.Failure(
                ServiceErrorType.NotFound,
                $"Event '{eventId}' was not found.");
        }

        var eventDateTime = evt.Date.ToDateTime(evt.Time);

        // BR - tickets cannot be purchased once the event has started.
        if (eventDateTime <= DateTime.Now)
        {
            return ServiceResult<TicketOrderResponse>.Failure(
                ServiceErrorType.Conflict,
                "Tickets cannot be purchased for an event that has already started.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return ServiceResult<TicketOrderResponse>.Failure(
                ServiceErrorType.NotFound,
                $"User '{userId}' was not found.");
        }

        // Idempotency:
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingOrder = await _db.TicketOrders
                .FirstOrDefaultAsync(
                    o => o.IdempotencyKey == idempotencyKey &&
                         o.EventId == eventId &&
                         o.CustomerEmail == user.Email,
                    ct);

            if (existingOrder is not null)
            {
                var existingTier = await _db.PricingTiers
                    .FirstOrDefaultAsync(t => t.Id == existingOrder.PricingTierId, ct);

                return ServiceResult<TicketOrderResponse>.Success(
                    new TicketOrderResponse
                    {
                        Id = existingOrder.Id,
                        EventId = existingOrder.EventId,
                        PricingTierId = existingOrder.PricingTierId,
                        PricingTierName = existingTier?.Name ?? string.Empty,
                        CustomerName = existingOrder.CustomerName,
                        CustomerEmail = existingOrder.CustomerEmail,
                        Quantity = existingOrder.Quantity,
                        UnitPrice = existingOrder.UnitPrice,
                        TotalPrice = existingOrder.TotalPrice,
                        PurchasedAtUtc = existingOrder.PurchasedAtUtc
                    });
            }
        }

        // BR - prevent overselling under concurrent purchases.
        for (var attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
        {
            var tier = await _db.PricingTiers
                .FirstOrDefaultAsync(t => t.Id == request.PricingTierId && t.EventId == eventId, ct);

            if (tier is null)
            {
                return ServiceResult<TicketOrderResponse>.Failure(
                    ServiceErrorType.NotFound, $"Pricing tier '{request.PricingTierId}' was not found for this event.");
            }
            // BR - prevent purchasing more tickets than remain in the pricing tier.
            if (tier.Remaining < request.Quantity)
            {
                return ServiceResult<TicketOrderResponse>.Failure(
                    ServiceErrorType.Conflict,
                    $"Not enough tickets remaining in tier '{tier.Name}'. Requested {request.Quantity}, remaining {tier.Remaining}.");
            }

            tier.TicketsSold += request.Quantity;

            var order = new TicketOrder
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                PricingTierId = tier.Id,
                IdempotencyKey = idempotencyKey,
                CustomerName = user.Username,
                CustomerEmail = user.Email,
                Quantity = request.Quantity,
                UnitPrice = tier.Price,
                TotalPrice = tier.Price * request.Quantity,
                PurchasedAtUtc = DateTime.UtcNow
            };

            _db.TicketOrders.Add(order);

            try
            {
                await _db.SaveChangesAsync(ct);

                return ServiceResult<TicketOrderResponse>.Success(new TicketOrderResponse
                {
                    Id = order.Id,
                    EventId = order.EventId,
                    PricingTierId = order.PricingTierId,
                    PricingTierName = tier.Name,
                    CustomerName = order.CustomerName,
                    CustomerEmail = order.CustomerEmail,
                    Quantity = order.Quantity,
                    UnitPrice = order.UnitPrice,
                    TotalPrice = order.TotalPrice,
                    PurchasedAtUtc = order.PurchasedAtUtc
                });
            }
            catch (DbUpdateException)
            {
                _db.Entry(order).State = EntityState.Detached;
                _db.Entry(tier).State = EntityState.Detached;
            }
        }

        return ServiceResult<TicketOrderResponse>.Failure(
                ServiceErrorType.Conflict,
                "Could not complete purchase due to high contention on this pricing tier. Please retry.");
    }

    // returns ticket availability by pricing tier and for the event overall.
    public async Task<ServiceResult<TicketAvailabilityResponse>> GetAvailabilityAsync(Guid eventId, CancellationToken ct = default)
    {
        var evt = await _db.Events.Include(e => e.PricingTiers)
            .FirstOrDefaultAsync(e => e.Id == eventId, ct);

        if (evt is null)
            return ServiceResult<TicketAvailabilityResponse>.Failure(ServiceErrorType.NotFound, $"Event '{eventId}' was not found.");

        var tiers = evt.PricingTiers.Select(t => new PricingTierResponse
        {
            Id = t.Id,
            Name = t.Name,
            Price = t.Price,
            Capacity = t.Capacity,
            TicketsSold = t.TicketsSold,
            Remaining = t.Remaining
        }).ToList();

        return ServiceResult<TicketAvailabilityResponse>.Success(new TicketAvailabilityResponse
        {
            EventId = evt.Id,
            TotalCapacity = evt.TotalCapacity,
            TotalSold = tiers.Sum(t => t.TicketsSold),
            TotalRemaining = tiers.Sum(t => t.Remaining),
            Tiers = tiers
        });
    }
}

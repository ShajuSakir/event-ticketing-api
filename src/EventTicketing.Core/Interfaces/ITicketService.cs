using EventTicketing.Core.Dtos;
using EventTicketing.Core.Results;

namespace EventTicketing.Core.Interfaces;

public interface ITicketService
{
    Task<ServiceResult<TicketOrderResponse>> PurchaseAsync(Guid eventId, Guid userId, PurchaseTicketRequest request, string? idempotencyKey = null, CancellationToken ct = default);
    Task<ServiceResult<TicketAvailabilityResponse>> GetAvailabilityAsync(Guid eventId, CancellationToken ct = default);
}

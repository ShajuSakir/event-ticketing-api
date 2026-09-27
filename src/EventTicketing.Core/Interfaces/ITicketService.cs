using EventTicketing.Core.Dtos;
using EventTicketing.Core.Results;

namespace EventTicketing.Core.Interfaces;

public interface ITicketService
{
    Task<ServiceResult<TicketOrderResponse>> PurchaseAsync(Guid eventId, Guid userId, PurchaseTicketRequest request, CancellationToken ct = default);
    Task<ServiceResult<TicketAvailabilityResponse>> GetAvailabilityAsync(Guid eventId, CancellationToken ct = default);
}

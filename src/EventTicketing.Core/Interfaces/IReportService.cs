using EventTicketing.Core.Dtos;
using EventTicketing.Core.Results;

namespace EventTicketing.Core.Interfaces;

public interface IReportService
{
    Task<ServiceResult<EventSalesSummary>> GetEventSalesSummaryAsync(Guid eventId, CancellationToken ct = default);
    Task<List<EventSalesSummary>> GetAllEventsSalesSummaryAsync(CancellationToken ct = default);
}

using EventTicketing.Core.Dtos;
using EventTicketing.Core.Results;

namespace EventTicketing.Core.Interfaces;

public interface IEventService
{
    Task<ServiceResult<EventResponse>> CreateAsync(CreateEventRequest request, CancellationToken ct = default);
    Task<ServiceResult<EventResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<EventResponse>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<ServiceResult<EventResponse>> UpdateAsync(Guid id, UpdateEventRequest request, CancellationToken ct = default);
    Task<ServiceResult<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}

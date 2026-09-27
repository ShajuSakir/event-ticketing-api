using EventTicketing.Core.Dtos;
using EventTicketing.Core.Results;

namespace EventTicketing.Core.Interfaces;

public interface IAuthService
{
    Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

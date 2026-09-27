using EventTicketing.Core.Dtos;
using EventTicketing.Core.Interfaces;
using EventTicketing.Core.Results;
using EventTicketing.Infrastructure.Data;
using EventTicketing.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly JwtTokenGenerator _tokenGenerator;

    public AuthService(AppDbContext db, JwtTokenGenerator tokenGenerator)
    {
        _db = db;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username, ct);

        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
            return ServiceResult<LoginResponse>.Failure(ServiceErrorType.Validation, "Invalid username or password.");

        var (token, expiresAtUtc) = _tokenGenerator.Generate(user);

        return ServiceResult<LoginResponse>.Success(new LoginResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            Username = user.Username,
            Role = user.Role
        });
    }
}

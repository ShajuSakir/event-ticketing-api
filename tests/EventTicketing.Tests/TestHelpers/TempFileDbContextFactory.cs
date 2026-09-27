using EventTicketing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Tests.TestHelpers;

public sealed class TempFileDbContextFactory : IDisposable
{
    private readonly string _dbPath;

    public TempFileDbContextFactory()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"eventticketing-test-{Guid.NewGuid():N}.db");

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath};Default Timeout=30")
            .Options;

        return new AppDbContext(options);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch (IOException) { /* Cleanup is best-effort; don't fail the test if the temporary file is still locked. */ }
        }
    }
}

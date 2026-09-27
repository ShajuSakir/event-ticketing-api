using EventTicketing.Core.Constants;
using EventTicketing.Core.Dtos;
using EventTicketing.Core.Entities;
using EventTicketing.Core.Results;
using EventTicketing.Infrastructure.Data;
using EventTicketing.Infrastructure.Services;
using EventTicketing.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace EventTicketing.Tests.Services;

public class TicketServiceTests : IDisposable
{
    private readonly InMemoryDbContextFactory _factory = new();

    private static async Task<Guid> SeedUserAsync(AppDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            Role = Roles.Customer,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }

    private async Task<(Guid eventId, Guid tierId)> SeedEventAsync(int tierCapacity)
    {
        using var db = _factory.CreateContext();
        var eventService = new EventService(db);

        var created = await eventService.CreateAsync(new CreateEventRequest
        {
            Name = "Sample Event",
            Venue = "Main Hall",
            Date = new DateOnly(2026, 12, 25),
            Time = new TimeOnly(18, 0),
            TotalCapacity = tierCapacity,
            PricingTiers = new List<PricingTierRequest>
            {
                new()
                {
                    Name = "General",
                    Price = 25m,
                    Capacity = tierCapacity
                }
            }
        });

        created.IsSuccess.Should().BeTrue();

        return (
            created.Value!.Id,
            created.Value.PricingTiers[0].Id
        );
    }

    [Fact]
    public async Task PurchaseAsync_ReducesRemainingCapacity()
    {
        var (eventId, tierId) = await SeedEventAsync(tierCapacity: 10);

        using var db = _factory.CreateContext();
        var userId = await SeedUserAsync(db);

        var ticketService = new TicketService(db);

        var result = await ticketService.PurchaseAsync(
            eventId,
            userId,
            new PurchaseTicketRequest
            {
                PricingTierId = tierId,
                Quantity = 4
            });

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalPrice.Should().Be(100m);

        var availability = await ticketService.GetAvailabilityAsync(eventId);

        availability.Value!.TotalRemaining.Should().Be(6);
    }

    [Fact]
    public async Task PurchaseAsync_ReturnsConflict_WhenQuantityExceedsRemaining()
    {
        var (eventId, tierId) = await SeedEventAsync(tierCapacity: 3);

        using var db = _factory.CreateContext();
        var userId = await SeedUserAsync(db);

        var ticketService = new TicketService(db);

        var result = await ticketService.PurchaseAsync(
            eventId,
            userId,
            new PurchaseTicketRequest
            {
                PricingTierId = tierId,
                Quantity = 4
            });

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ServiceErrorType.Conflict);
    }

    [Fact]
    public async Task PurchaseAsync_ReturnsNotFound_ForUnknownEvent()
    {
        using var db = _factory.CreateContext();

        var ticketService = new TicketService(db);

        var result = await ticketService.PurchaseAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new PurchaseTicketRequest
            {
                PricingTierId = Guid.NewGuid(),
                Quantity = 1
            });

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ServiceErrorType.NotFound);
    }

    [Fact]
    public async Task PurchaseAsync_NeverOversells_UnderConcurrentRequests()
    {
        // 20 concurrent buyers, each requesting 1 ticket, against a tier with only 10 seats.
        //
        // Uses a real on-disk SQLite file via TempFileDbContextFactory because the
        // in-memory provider's shared connection cannot safely handle concurrent
        // access from multiple threads.
        const int capacity = 10;
        const int concurrentBuyers = 20;

        using var fileFactory = new TempFileDbContextFactory();

        Guid eventId;
        Guid tierId;
        Guid userId;

        using (var seedDb = fileFactory.CreateContext())
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                Email = "test@example.com",
                Role = Roles.Customer,
                CreatedAtUtc = DateTime.UtcNow
            };

            seedDb.Users.Add(user);

            var created = await new EventService(seedDb).CreateAsync(
                new CreateEventRequest
                {
                    Name = "High Demand Event",
                    Venue = "Main Hall",
                    Date = new DateOnly(2026, 12, 25),
                    Time = new TimeOnly(18, 0),
                    TotalCapacity = capacity,
                    PricingTiers = new List<PricingTierRequest>
                    {
                        new()
                        {
                            Name = "General",
                            Price = 25m,
                            Capacity = capacity
                        }
                    }
                });

            created.IsSuccess.Should().BeTrue();

            await seedDb.SaveChangesAsync();

            eventId = created.Value!.Id;
            tierId = created.Value.PricingTiers[0].Id;
            userId = user.Id;
        }

        var tasks = Enumerable.Range(0, concurrentBuyers).Select(async i =>
        {
            using var db = fileFactory.CreateContext();
            var ticketService = new TicketService(db);

            return await ticketService.PurchaseAsync(
                eventId,
                userId,
                new PurchaseTicketRequest
                {
                    PricingTierId = tierId,
                    Quantity = 1
                });
        });

        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r.IsSuccess);

        var conflictCount = results.Count(
            r => !r.IsSuccess &&
                 r.ErrorType == ServiceErrorType.Conflict);

        successCount.Should().Be(capacity);
        conflictCount.Should().Be(concurrentBuyers - capacity);

        using var verifyDb = fileFactory.CreateContext();

        var finalAvailability =
            await new TicketService(verifyDb)
                .GetAvailabilityAsync(eventId);

        finalAvailability.Value!.TotalRemaining.Should().Be(0);
        finalAvailability.Value.TotalSold.Should().Be(capacity);
    }

    [Fact]
    public async Task PurchaseAsync_ReturnsConflict_WhenEventHasAlreadyStarted()
    {
        using var db = _factory.CreateContext();

        var userId = await SeedUserAsync(db);

        var eventService = new EventService(db);

        var pastEvent = DateTime.Now.AddHours(-1);

        var createResult = await eventService.CreateAsync(
            new CreateEventRequest
            {
                Name = "Past Event",
                Venue = "Main Hall",
                Date = DateOnly.FromDateTime(pastEvent),
                Time = TimeOnly.FromDateTime(pastEvent),
                TotalCapacity = 10,
                PricingTiers = new List<PricingTierRequest>
                {
                    new()
                    {
                        Name = "General",
                        Price = 25m,
                        Capacity = 10
                    }
                }
            });

        createResult.IsSuccess.Should().BeTrue();

        var created = createResult.Value!;

        var ticketService = new TicketService(db);

        var result = await ticketService.PurchaseAsync(
            created.Id,
            userId,
            new PurchaseTicketRequest
            {
                PricingTierId = created.PricingTiers[0].Id,
                Quantity = 1
            });

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ServiceErrorType.Conflict);
        result.ErrorMessage.Should().Contain("already started");
    }

    public void Dispose() => _factory.Dispose();
}


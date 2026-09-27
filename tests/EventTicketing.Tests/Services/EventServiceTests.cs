using EventTicketing.Core.Constants;
using EventTicketing.Core.Dtos;
using EventTicketing.Core.Entities;
using EventTicketing.Core.Results;
using EventTicketing.Infrastructure.Services;
using EventTicketing.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace EventTicketing.Tests.Services;

public class EventServiceTests : IDisposable
{
    private readonly InMemoryDbContextFactory _factory = new();

    private static async Task<User> SeedUserAsync(
        EventTicketing.Infrastructure.Data.AppDbContext db)
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

        return user;
    }

    private static CreateEventRequest CreateEventRequest(
        string name,
        string venue,
        int totalCapacity)
    {
        return new CreateEventRequest
        {
            Name = name,
            Venue = venue,
            Date = new DateOnly(2026, 12, 1),
            Time = new TimeOnly(20, 0),
            TotalCapacity = totalCapacity,
            PricingTiers = new List<PricingTierRequest>
            {
                new()
                {
                    Name = "Standard",
                    Price = 30m,
                    Capacity = totalCapacity
                }
            }
        };
    }

    [Fact]
    public async Task CreateAsync_PersistsEventWithPricingTiers()
    {
        using var db = _factory.CreateContext();
        var service = new EventService(db);

        var request = new CreateEventRequest
        {
            Name = "Tech Conf 2026",
            Description = "Annual tech conference",
            Venue = "Convention Center",
            Date = new DateOnly(2026, 11, 5),
            Time = new TimeOnly(9, 0),
            TotalCapacity = 100,
            PricingTiers = new List<PricingTierRequest>
            {
                new() { Name = "General", Price = 50m, Capacity = 80 },
                new() { Name = "VIP", Price = 150m, Capacity = 20 }
            }
        };

        var result = await service.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        result.Value!.Id.Should().NotBeEmpty();
        result.Value.PricingTiers.Should().HaveCount(2);
        result.Value.PricingTiers.Should()
            .Contain(t => t.Name == "General" && t.Remaining == 80);
    }

    [Fact]
    public async Task CreateAsync_RejectsWhenPricingTierCapacityDoesNotMatchEventCapacity()
    {
        using var db = _factory.CreateContext();
        var service = new EventService(db);

        var request = new CreateEventRequest
        {
            Name = "Tech Conf 2026",
            Description = "Annual tech conference",
            Venue = "Convention Center",
            Date = new DateOnly(2026, 11, 5),
            Time = new TimeOnly(9, 0),
            TotalCapacity = 100,
            PricingTiers = new List<PricingTierRequest>
            {
                new() { Name = "General", Price = 50m, Capacity = 80 },
                new() { Name = "VIP", Price = 150m, Capacity = 50 }
            }
        };

        var result = await service.CreateAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ServiceErrorType.Validation);
        result.ErrorMessage.Should().Contain("pricing tiers");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotFound_WhenEventDoesNotExist()
    {
        using var db = _factory.CreateContext();
        var service = new EventService(db);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ServiceErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_RejectsCapacityBelowTicketsSold()
    {
        using var db = _factory.CreateContext();

        var user = await SeedUserAsync(db);
        var eventService = new EventService(db);
        var ticketService = new TicketService(db);

        var createResult = await eventService.CreateAsync(
            CreateEventRequest(
                name: "Concert",
                venue: "Arena",
                totalCapacity: 10));

        createResult.IsSuccess.Should().BeTrue();

        var created = createResult.Value!;

        await ticketService.PurchaseAsync(
            created.Id,
            user.Id,
            new PurchaseTicketRequest
            {
                PricingTierId = created.PricingTiers[0].Id,
                Quantity = 5
            });

        var updateResult = await eventService.UpdateAsync(
            created.Id,
            new UpdateEventRequest
            {
                Name = "Concert",
                Venue = "Arena",
                Date = new DateOnly(2026, 12, 1),
                Time = new TimeOnly(20, 0),

                // Trying to reduce capacity below
                // the 5 tickets already sold.
                TotalCapacity = 3
            });

        updateResult.IsSuccess.Should().BeFalse();
        updateResult.ErrorType.Should().Be(ServiceErrorType.Validation);
    }

    [Fact]
    public async Task DeleteAsync_RejectsDeletionWhenTicketsAlreadySold()
    {
        using var db = _factory.CreateContext();

        var user = await SeedUserAsync(db);
        var eventService = new EventService(db);
        var ticketService = new TicketService(db);

        var createResult = await eventService.CreateAsync(
            CreateEventRequest(
                name: "Workshop",
                venue: "Hall B",
                totalCapacity: 5));

        createResult.IsSuccess.Should().BeTrue();

        var created = createResult.Value!;

        await ticketService.PurchaseAsync(
            created.Id,
            user.Id,
            new PurchaseTicketRequest
            {
                PricingTierId = created.PricingTiers[0].Id,
                Quantity = 1
            });

        var deleteResult = await eventService.DeleteAsync(created.Id);

        deleteResult.IsSuccess.Should().BeFalse();
        deleteResult.ErrorType.Should().Be(ServiceErrorType.Conflict);
    }   

    public void Dispose() => _factory.Dispose();
}
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EventTicketing.Core.Dtos;
using FluentAssertions;
using Xunit;

namespace EventTicketing.Tests.Integration;

public class EventTicketingApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public EventTicketingApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> LoginAsync(string username, string password)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Username = username,
                Password = password
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();

        login.Should().NotBeNull();
        login!.Token.Should().NotBeNullOrWhiteSpace();

        return login.Token;
    }

    private async Task AuthenticateAsAsync(string username, string password)
    {
        var token = await LoginAsync(username, password);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private void ClearAuthentication()
    {
        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task UnauthenticatedUser_CannotPurchaseTickets()
    {
        ClearAuthentication();

        var response = await _client.PostAsJsonAsync(
            $"/api/events/{Guid.NewGuid()}/tickets/purchase",
            new PurchaseTicketRequest
            {
                PricingTierId = Guid.NewGuid(),
                Quantity = 1
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Customer_CanPurchaseTickets()
    {
        // create the event as admin.
        await AuthenticateAsAsync("admin", "Admin123!");

        var createResponse = await _client.PostAsJsonAsync(
            "/api/events",
            new CreateEventRequest
            {
                Name = "Customer Purchase Test",
                Venue = "Test Arena",
                Date = new DateOnly(2027, 5, 10),
                Time = new TimeOnly(19, 0),
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

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created =
            await createResponse.Content.ReadFromJsonAsync<EventResponse>();

        created.Should().NotBeNull();

        var tierId = created!.PricingTiers[0].Id;

        // purchase as customer.
        await AuthenticateAsAsync("customer", "Customer123!");

        var purchaseResponse = await _client.PostAsJsonAsync(
            $"/api/events/{created.Id}/tickets/purchase",
            new PurchaseTicketRequest
            {
                PricingTierId = tierId,
                Quantity = 2
            });

        purchaseResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var order =
            await purchaseResponse.Content.ReadFromJsonAsync<TicketOrderResponse>();

        order.Should().NotBeNull();
        order!.Quantity.Should().Be(2);
        order.TotalPrice.Should().Be(50m);

        order.CustomerName.Should().Be("customer");
        order.CustomerEmail.Should().Be("customer@example.com");
    }

    [Fact]
    public async Task Customer_CannotCreateEvent()
    {
        await AuthenticateAsAsync("customer", "Customer123!");

        var response = await _client.PostAsJsonAsync(
            "/api/events",
            new CreateEventRequest
            {
                Name = "Customer Should Not Create",
                Venue = "Test Arena",
                Date = new DateOnly(2027, 6, 10),
                Time = new TimeOnly(19, 0),
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

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_CanCreateEvent()
    {
        await AuthenticateAsAsync("admin", "Admin123!");

        var response = await _client.PostAsJsonAsync(
            "/api/events",
            new CreateEventRequest
            {
                Name = "Admin Created Event",
                Venue = "Convention Center",
                Date = new DateOnly(2027, 7, 10),
                Time = new TimeOnly(19, 0),
                TotalCapacity = 100,
                PricingTiers = new List<PricingTierRequest>
                {
                    new()
                    {
                        Name = "General",
                        Price = 50m,
                        Capacity = 80
                    },
                    new()
                    {
                        Name = "VIP",
                        Price = 150m,
                        Capacity = 20
                    }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created =
            await response.Content.ReadFromJsonAsync<EventResponse>();

        created.Should().NotBeNull();
        created!.Name.Should().Be("Admin Created Event");
    }

    [Fact]
    public async Task Customer_CannotAccessSalesReport()
    {
        await AuthenticateAsAsync("customer", "Customer123!");

        var response = await _client.GetAsync("/api/reports/sales");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnauthenticatedUser_CannotAccessSalesReport()
    {
        ClearAuthentication();

        var response = await _client.GetAsync("/api/reports/sales");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_CanAccessSalesReport()
    {
        await AuthenticateAsAsync("admin", "Admin123!");

        var response = await _client.GetAsync("/api/reports/sales");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summaries =
            await response.Content.ReadFromJsonAsync<List<EventSalesSummary>>();

        summaries.Should().NotBeNull();
    }

    [Fact]
    public async Task FullFlow_CreateEvent_PurchaseTickets_ViewReport()
    {
        // admin creates the event.
        await AuthenticateAsAsync("admin", "Admin123!");

        var createRequest = new CreateEventRequest
        {
            Name = "Integration Test Concert",
            Description = "End-to-end test event",
            Venue = "Test Arena",
            Date = new DateOnly(2027, 1, 15),
            Time = new TimeOnly(19, 30),
            TotalCapacity = 50,
            PricingTiers = new List<PricingTierRequest>
            {
                new()
                {
                    Name = "Standard",
                    Price = 40m,
                    Capacity = 40
                },
                new()
                {
                    Name = "VIP",
                    Price = 120m,
                    Capacity = 10
                }
            }
        };

        var createResponse =
            await _client.PostAsJsonAsync(
                "/api/events",
                createRequest);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created =
            await createResponse.Content.ReadFromJsonAsync<EventResponse>();

        created.Should().NotBeNull();

        var standardTierId =
            created!.PricingTiers
                .First(t => t.Name == "Standard")
                .Id;

        // customer purchases tickets.
        await AuthenticateAsAsync("customer", "Customer123!");

        var purchaseResponse =
            await _client.PostAsJsonAsync(
                $"/api/events/{created.Id}/tickets/purchase",
                new PurchaseTicketRequest
                {
                    PricingTierId = standardTierId,
                    Quantity = 3
                });

        purchaseResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var order =
            await purchaseResponse.Content
                .ReadFromJsonAsync<TicketOrderResponse>();

        order.Should().NotBeNull();
        order!.TotalPrice.Should().Be(120m);

        // availability is public.
        var availabilityResponse =
            await _client.GetAsync(
                $"/api/events/{created.Id}/tickets/availability");

        availabilityResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var availability =
            await availabilityResponse.Content
                .ReadFromJsonAsync<TicketAvailabilityResponse>();

        availability.Should().NotBeNull();
        availability!.TotalSold.Should().Be(3);

        // sales report requires admin.
        await AuthenticateAsAsync("admin", "Admin123!");

        var reportResponse =
            await _client.GetAsync(
                $"/api/reports/sales/{created.Id}");

        reportResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary =
            await reportResponse.Content
                .ReadFromJsonAsync<EventSalesSummary>();

        summary.Should().NotBeNull();
        summary!.TotalRevenue.Should().Be(120m);
        summary.OrderCount.Should().Be(1);
    }

    [Fact]
    public async Task Purchase_ReturnsConflict_WhenOversellingAttempted()
    {
        // create event as admin.
        await AuthenticateAsAsync("admin", "Admin123!");

        var createRequest = new CreateEventRequest
        {
            Name = "Small Venue Show",
            Venue = "Tiny Room",
            Date = new DateOnly(2027, 2, 1),
            Time = new TimeOnly(20, 0),
            TotalCapacity = 2,
            PricingTiers = new List<PricingTierRequest>
            {
                new()
                {
                    Name = "GA",
                    Price = 15m,
                    Capacity = 2
                }
            }
        };

        var createResponse =
            await _client.PostAsJsonAsync(
                "/api/events",
                createRequest);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<EventResponse>();

        created.Should().NotBeNull();

        var tierId = created!.PricingTiers[0].Id;

        // purchase as customer.
        await AuthenticateAsAsync("customer", "Customer123!");

        var purchaseResponse =
            await _client.PostAsJsonAsync(
                $"/api/events/{created.Id}/tickets/purchase",
                new PurchaseTicketRequest
                {
                    PricingTierId = tierId,
                    Quantity = 5
                });

        purchaseResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetEvent_ReturnsNotFound_ForUnknownId()
    {
        ClearAuthentication();

        var response =
            await _client.GetAsync(
                $"/api/events/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateEvent_ReturnsBadRequest_WhenValidationFails()
    {
        await AuthenticateAsAsync("admin", "Admin123!");

        var invalidRequest = new CreateEventRequest
        {
            Name = "",
            Venue = "",
            Date = new DateOnly(2027, 1, 1),
            Time = new TimeOnly(10, 0),
            TotalCapacity = 0,
            PricingTiers = new List<PricingTierRequest>()
        };

        var response =
            await _client.PostAsJsonAsync(
                "/api/events",
                invalidRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
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

   
    // Authentication helpers  

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


    // Integration test helpers

    private async Task<EventResponse> CreateEventAsAdminAsync(
        string name,
        int totalCapacity,
        List<PricingTierRequest> pricingTiers,
        string description = "Integration test event")
    {
        await AuthenticateAsAsync("admin", "Admin123!");

        var response = await _client.PostAsJsonAsync(
            "/api/events",
            new CreateEventRequest
            {
                Name = name,
                Description = description,
                Venue = "Test Arena",
                Date = new DateOnly(2027, 6, 10),
                Time = new TimeOnly(19, 0),
                TotalCapacity = totalCapacity,
                PricingTiers = pricingTiers
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created =
            await response.Content.ReadFromJsonAsync<EventResponse>();

        created.Should().NotBeNull();

        return created!;
    }

    private async Task<TicketOrderResponse> PurchaseTicketsAsync(
        Guid eventId,
        Guid pricingTierId,
        int quantity,
        string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/events/{eventId}/tickets/purchase");

        request.Content = JsonContent.Create(
            new PurchaseTicketRequest
            {
                PricingTierId = pricingTierId,
                Quantity = quantity
            });

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var order =
            await response.Content.ReadFromJsonAsync<TicketOrderResponse>();

        order.Should().NotBeNull();

        return order!;
    }


    // Authentication / authorization

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
        var created = await CreateEventAsAdminAsync(
            "Admin Created Event",
            100,
            new List<PricingTierRequest>
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
            }, 
            "Admin created integration test event");

        created.Name.Should().Be("Admin Created Event");
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


    // Ticket purchasing

    [Fact]
    public async Task Customer_CanPurchaseTickets()
    {
        var created = await CreateEventAsAdminAsync(
            "Customer Purchase Test",
            10,
            new List<PricingTierRequest>
            {
                new()
                {
                    Name = "General",
                    Price = 25m,
                    Capacity = 10
                }
            },
            "Customer purchase integration test event");

        var tierId = created.PricingTiers[0].Id;

        await AuthenticateAsAsync("customer", "Customer123!");

        var order = await PurchaseTicketsAsync(
            created.Id,
            tierId,
            2);

        order.Quantity.Should().Be(2);
        order.TotalPrice.Should().Be(50m);
        order.CustomerName.Should().Be("customer");
        order.CustomerEmail.Should().Be("customer@example.com");
    }

    [Fact]
    public async Task Purchase_ReturnsConflict_WhenOversellingAttempted()
    {
        var created = await CreateEventAsAdminAsync(
            "Small Venue Show",
            2,
            new List<PricingTierRequest>
            {
                new()
                {
                    Name = "GA",
                    Price = 15m,
                    Capacity = 2
                }
            },
            "Idempotency integration test event");

        var tierId = created.PricingTiers[0].Id;

        await AuthenticateAsAsync("customer", "Customer123!");

        var response =
            await _client.PostAsJsonAsync(
                $"/api/events/{created.Id}/tickets/purchase",
                new PurchaseTicketRequest
                {
                    PricingTierId = tierId,
                    Quantity = 5
                });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Customer_RepeatingSameIdempotencyKey_ReturnsSameOrder()
    {
        var created = await CreateEventAsAdminAsync(
            "Idempotency Test",
            10,
            new List<PricingTierRequest>
            {
                new()
                {
                    Name = "General",
                    Price = 25m,
                    Capacity = 10
                }
            },
            "Idempotency integration test event");

        var tierId = created.PricingTiers[0].Id;

        await AuthenticateAsAsync("customer", "Customer123!");

        var idempotencyKey = Guid.NewGuid().ToString();

        var firstOrder = await PurchaseTicketsAsync(
            created.Id,
            tierId,
            2,
            idempotencyKey);

        var secondOrder = await PurchaseTicketsAsync(
            created.Id,
            tierId,
            2,
            idempotencyKey);

        // Same idempotency key should return the original order.
        secondOrder.Id.Should().Be(firstOrder.Id);
        secondOrder.Quantity.Should().Be(firstOrder.Quantity);
        secondOrder.TotalPrice.Should().Be(firstOrder.TotalPrice);

        // The retry must not consume another 2 tickets.
        var availabilityResponse =
            await _client.GetAsync(
                $"/api/events/{created.Id}/tickets/availability");

        availabilityResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var availability =
            await availabilityResponse.Content
                .ReadFromJsonAsync<TicketAvailabilityResponse>();

        availability.Should().NotBeNull();
        availability!.TotalSold.Should().Be(2);
    }


    // End-to-end flow

    [Fact]
    public async Task FullFlow_CreateEvent_PurchaseTickets_ViewReport()
    {
        var created = await CreateEventAsAdminAsync(
            "Integration Test Concert",
            50,
            new List<PricingTierRequest>
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
            },
            "End-to-end test event");

        var standardTierId =
            created.PricingTiers
                .First(t => t.Name == "Standard")
                .Id;

        await AuthenticateAsAsync("customer", "Customer123!");

        var order = await PurchaseTicketsAsync(
            created.Id,
            standardTierId,
            3);

        order.TotalPrice.Should().Be(120m);

        // Availability is public.
        var availabilityResponse =
            await _client.GetAsync(
                $"/api/events/{created.Id}/tickets/availability");

        availabilityResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var availability =
            await availabilityResponse.Content
                .ReadFromJsonAsync<TicketAvailabilityResponse>();

        availability.Should().NotBeNull();
        availability!.TotalSold.Should().Be(3);

        // Sales report requires admin.
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


    // Event / validation

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
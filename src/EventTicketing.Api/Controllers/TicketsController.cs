using EventTicketing.Api.Extensions;
using EventTicketing.Core.Dtos;
using EventTicketing.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EventTicketing.Api.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/tickets")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;

    public TicketsController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    //POST /api/events/{eventId}/tickets/purchase, buys tickets from a pricing tier
    [HttpPost("purchase")]
    [Authorize]
    [ProducesResponseType(typeof(TicketOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Purchase(Guid eventId, [FromBody] PurchaseTicketRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await _ticketService.PurchaseAsync(eventId, userId, request, ct);
        if (!result.IsSuccess)
            return result.ToActionResult(this);

        return CreatedAtAction(nameof(Purchase), new { eventId }, result.Value);
    }

    // GET /api/events/{eventId}/tickets/availability - public. returns remaining capacity per tier
    [HttpGet("availability")]
    [ProducesResponseType(typeof(TicketAvailabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailability(Guid eventId, CancellationToken ct)
    {
        var result = await _ticketService.GetAvailabilityAsync(eventId, ct);
        return result.ToActionResult(this);
    }
}

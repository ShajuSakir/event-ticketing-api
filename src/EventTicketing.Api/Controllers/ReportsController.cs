using EventTicketing.Api.Extensions;
using EventTicketing.Core.Constants;
using EventTicketing.Core.Dtos;
using EventTicketing.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventTicketing.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = Roles.Admin)]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    // GET /api/reports/sales - Admin-only. sales summary across all events
    [HttpGet("sales")]
    [ProducesResponseType(typeof(List<EventSalesSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllSalesSummaries(CancellationToken ct)
    {
        var summaries = await _reportService.GetAllEventsSalesSummaryAsync(ct);
        return Ok(summaries);
    }

    // GET /api/reports/sales/{eventId} - Admin-only. sales summary for a single event
    [HttpGet("sales/{eventId:guid}")]
    [ProducesResponseType(typeof(EventSalesSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSalesSummary(Guid eventId, CancellationToken ct)
    {
        var result = await _reportService.GetEventSalesSummaryAsync(eventId, ct);
        return result.ToActionResult(this);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OOAD.Application.DTOs.Report;
using OOAD.Application.Services.Interfaces;

namespace OOAD.API.Controllers;

[Authorize(Roles = "ADMIN,Admin")]
public class ReportsController : ApiControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("overview")]
    [ProducesResponseType(typeof(ReportOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportOverviewDto>> GetOverview(
        [FromQuery] Guid? branchId = null,
        [FromQuery] int months = 6,
        CancellationToken cancellationToken = default)
    {
        var data = await _reportService.GetOverviewAsync(branchId, months, cancellationToken);
        return Ok(data);
    }
}
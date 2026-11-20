using System.Security.Claims;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OOAD.API.Controllers;

[Route("api/body-measurements")]
public class BodyMeasurementsController : ApiControllerBase
{
    private readonly IBodyMeasurementService _measurementService;

    public BodyMeasurementsController(IBodyMeasurementService measurementService)
    {
        _measurementService = measurementService;
    }

    /// <summary>
    /// GET /api/body-measurements/me
    /// Lấy lịch sử đo chỉ số cơ thể của hội viên đang đăng nhập.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<BodyMeasurementDto>>> GetMyMeasurements(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var list = await _measurementService.GetMemberMeasurementsAsync(userId.Value, cancellationToken);
        return Ok(list);
    }

    /// <summary>
    /// POST /api/body-measurements
    /// Thêm mốc đo chỉ số cơ thể mới (Cân nặng, BMI, mỡ, cơ, các vòng đo...).
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<BodyMeasurementDto>> CreateMeasurement([FromBody] CreateBodyMeasurementRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var created = await _measurementService.CreateMeasurementAsync(userId.Value, request, cancellationToken);
        return Ok(created);
    }

    /// <summary>
    /// GET /api/body-measurements/metric-types
    /// Lấy danh sách các loại chỉ số đo lường đang kích hoạt trong hệ thống.
    /// </summary>
    [HttpGet("metric-types")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<MetricTypeDto>>> GetMetricTypes(CancellationToken cancellationToken)
    {
        var list = await _measurementService.GetMetricTypesAsync(cancellationToken);
        return Ok(list);
    }

    /// <summary>
    /// GET /api/body-measurements/nutrition-plans
    /// Lấy danh sách kế hoạch dinh dưỡng do HLV thiết lập cho hội viên đang đăng nhập.
    /// </summary>
    [HttpGet("nutrition-plans")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<NutritionPlanDto>>> GetNutritionPlans(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var list = await _measurementService.GetNutritionPlansAsync(userId.Value, cancellationToken);
        return Ok(list);
    }

    /// <summary>
    /// POST /api/body-measurements/nutrition-plans
    /// HLV thiết lập kế hoạch dinh dưỡng cho hội viên.
    /// </summary>
    [HttpPost("nutrition-plans")]
    [Authorize(Roles = "Trainer,Admin")]
    public async Task<ActionResult<NutritionPlanDto>> CreateNutritionPlan([FromBody] CreateNutritionPlanRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var created = await _measurementService.CreateNutritionPlanAsync(userId.Value, request, cancellationToken);
        return Ok(created);
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value
                    ?? User.FindFirst("id")?.Value;

        if (Guid.TryParse(claim, out var guid))
        {
            return guid;
        }

        return null;
    }
}

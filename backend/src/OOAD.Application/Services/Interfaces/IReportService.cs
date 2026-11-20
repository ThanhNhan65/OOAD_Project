using OOAD.Application.DTOs.Report;

namespace OOAD.Application.Services.Interfaces;

public interface IReportService
{
    Task<ReportOverviewDto> GetOverviewAsync(Guid? branchId = null, int months = 6, CancellationToken cancellationToken = default);
}
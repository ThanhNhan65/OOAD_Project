using OOAD.Application.DTOs;

namespace OOAD.Application.Services.Interfaces;

public interface ICheckinService
{
    Task<CheckinOperationResultDto> CheckInByCardAsync(CardCheckinRequest request, CancellationToken cancellationToken = default);
    Task<CheckinOperationResultDto> CheckInManualAsync(ManualCheckinRequest request, Guid staffUserId, CancellationToken cancellationToken = default);
    Task<CheckinOperationResultDto> CheckOutByCardAsync(CardCheckoutRequest request, CancellationToken cancellationToken = default);
    Task<CheckinOperationResultDto> CheckOutByIdAsync(Guid checkinId, CancellationToken cancellationToken = default);
    Task<CheckinOperationResultDto> CheckOutManualAsync(ManualCheckoutRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CheckinDto>> GetActiveCheckinsAsync(Guid? branchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CheckinDto>> GetHistoryAsync(
        Guid? branchId,
        string? method,
        DateTime? date,
        string? search,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CheckinDto>> GetMyHistoryAsync(Guid currentUserId, CancellationToken cancellationToken = default);
    Task<StaffDashboardDto> GetStaffDashboardAsync(Guid staffUserId, Guid? overrideBranchId = null, CancellationToken cancellationToken = default);
    Task<int> AutoCheckoutExpiredSessionsAsync(CancellationToken cancellationToken = default);
}
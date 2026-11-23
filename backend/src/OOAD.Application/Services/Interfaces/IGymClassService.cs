using OOAD.Application.DTOs;

namespace OOAD.Application.Services.Interfaces;

public interface IGymClassService
{
    Task<IReadOnlyList<GymClassDto>> GetAllClassesAsync(
        Guid? branchId = null,
        Guid? trainerId = null,
        CancellationToken cancellationToken = default);

    Task<GymClassDto> GetClassByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<GymClassDto> CreateClassAsync(
        CreateGymClassRequest request,
        CancellationToken cancellationToken = default);

    Task<GymClassDto> UpdateClassAsync(
        Guid id,
        UpdateGymClassRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteClassAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassSessionDto>> GenerateSessionsAsync(
        Guid classId,
        GenerateSessionsRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassSessionDto>> GetSessionsAsync(
        Guid? branchId = null,
        Guid? classId = null,
        Guid? trainerId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? status = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<ClassSessionDetailDto> GetSessionDetailAsync(
        Guid sessionId,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassSessionDto>> GetTrainerSessionsAsync(
        Guid currentUserId,
        string? filter = null,
        CancellationToken cancellationToken = default);

    Task<ClassSessionDto> RescheduleSessionAsync(
        Guid sessionId,
        RescheduleSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<ClassSessionDto> CancelSessionAsync(
        Guid sessionId,
        CancelSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<ConfirmAttendanceResultDto> ConfirmAttendanceAsync(
        Guid sessionId,
        Guid currentUserId,
        string currentRole,
        ConfirmAttendanceRequest request,
        CancellationToken cancellationToken = default);
}

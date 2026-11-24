using OOAD.Application.DTOs.Equipment;

namespace OOAD.Application.Services.Interfaces;

public interface IEquipmentService
{
    Task<IEnumerable<EquipmentDto>> GetAllAsync(Guid? branchId, CancellationToken ct = default);
    Task<EquipmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<EquipmentDto> CreateAsync(CreateEquipmentDto dto, CancellationToken ct = default);
    Task<EquipmentDto?> UpdateStatusAsync(Guid id, string status, CancellationToken ct = default);

    Task<IEnumerable<EquipmentReportDto>> GetReportsAsync(Guid? equipmentId, Guid? branchId, CancellationToken ct = default);
    Task<EquipmentReportDto> CreateReportAsync(Guid userId, string userRole, CreateEquipmentReportDto dto, CancellationToken ct = default);
    Task<EquipmentReportDto?> UpdateReportStatusAsync(Guid reportId, Guid userId, string userRole, UpdateReportStatusDto dto, CancellationToken ct = default);
}
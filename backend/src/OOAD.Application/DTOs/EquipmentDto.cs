namespace OOAD.Application.DTOs.Equipment;

public record EquipmentDto(
    Guid Id,
    Guid BranchId,
    string Name,
    string Category,
    string Code,
    string Status,
    DateTime AddedAt
);

public record CreateEquipmentDto(
    Guid BranchId,
    string Name,
    string Category
);

public record UpdateEquipmentStatusDto(
    string Status
);

public record EquipmentReportUpdateDto(
    DateTime At,
    Guid By,
    string Note,
    string Status
);

public record EquipmentReportDto(
    Guid Id,
    Guid EquipmentId,
    Guid BranchId,
    Guid ReporterId,
    string Description,
    string Status,
    DateTime CreatedAt,
    List<EquipmentReportUpdateDto> Updates
);

public record CreateEquipmentReportDto(
    Guid EquipmentId,
    string Description
);

public record UpdateReportStatusDto(
    string Status,
    string Note
);
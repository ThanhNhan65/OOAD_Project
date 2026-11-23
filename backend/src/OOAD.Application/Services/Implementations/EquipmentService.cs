using OOAD.Application.DTOs.Equipment;
using OOAD.Application.Services.Interfaces;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Exceptions;
using OOAD.Domain.Interfaces;

namespace OOAD.Application.Services.Implementations;

public class EquipmentService : IEquipmentService
{
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IEquipmentIssueRepository _equipmentIssueRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IStaffRepository _staffRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public EquipmentService(
        IEquipmentRepository equipmentRepository,
        IEquipmentIssueRepository equipmentIssueRepository,
        IBranchRepository branchRepository,
        IMemberRepository memberRepository,
        IStaffRepository staffRepository,
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork)
    {
        _equipmentRepository = equipmentRepository;
        _equipmentIssueRepository = equipmentIssueRepository;
        _branchRepository = branchRepository;
        _memberRepository = memberRepository;
        _staffRepository = staffRepository;
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<EquipmentDto>> GetAllAsync(Guid? branchId, CancellationToken ct = default)
    {
        var list = await _equipmentRepository.GetAllByBranchAsync(branchId, ct);
        return list.Select(MapEquipment);
    }

    public async Task<EquipmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var eq = await _equipmentRepository.GetByIdAsync(id, ct);
        return eq == null ? null : MapEquipment(eq);
    }

    public async Task<EquipmentDto> CreateAsync(CreateEquipmentDto dto, CancellationToken ct = default)
    {
        var branchExists = await _branchRepository.ExistsAsync(dto.BranchId, ct);
        if (!branchExists)
            throw new NotFoundException("Chi nhánh không tồn tại.");

        var eq = new Equipment
        {
            BranchId = dto.BranchId,
            Name = dto.Name.Trim(),
            Type = dto.Category.Trim(),
            Status = EquipmentStatus.Operational
        };

        await _equipmentRepository.AddAsync(eq, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapEquipment(eq);
    }

    public async Task<EquipmentDto?> UpdateStatusAsync(Guid id, string status, CancellationToken ct = default)
    {
        var eq = await _equipmentRepository.GetByIdAsync(id, ct);
        if (eq == null) return null;

        eq.Status = ParseEquipmentStatus(status);
        _equipmentRepository.Update(eq);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapEquipment(eq);
    }

    public async Task<IEnumerable<EquipmentReportDto>> GetReportsAsync(Guid? equipmentId, Guid? branchId, CancellationToken ct = default)
    {
        var issues = await _equipmentIssueRepository.GetReportsWithDetailsAsync(equipmentId, branchId, ct);
        return issues.Select(MapReport);
    }

    public async Task<EquipmentReportDto> CreateReportAsync(Guid userId, CreateEquipmentReportDto dto, CancellationToken ct = default)
    {
        var eq = await _equipmentRepository.GetByIdAsync(dto.EquipmentId, ct)
            ?? throw new NotFoundException("Thiết bị không tồn tại.");

        var member = await _memberRepository.GetByUserIdWithUserAsync(userId, asNoTracking: true, ct)
            ?? throw new ValidationException("Chỉ hội viên mới được gửi phản ánh sự cố.");

        var issue = new EquipmentIssue
        {
            EquipmentId = eq.Id,
            ReportedBy = member.Id,
            Description = dto.Description.Trim(),
            Status = IssueStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        // Đánh dấu thiết bị gặp sự cố
        eq.Status = EquipmentStatus.Broken;
        _equipmentRepository.Update(eq);
        await _equipmentIssueRepository.AddAsync(issue, ct);

        // Tự động gửi thông báo cho Lễ tân thuộc chi nhánh có thiết bị hỏng
        var branchStaffUserIds = await _staffRepository.GetStaffUserIdsByBranchIdAsync(eq.BranchId, ct);

        foreach (var staffUserId in branchStaffUserIds)
        {
            await _notificationRepository.AddAsync(new Notification
            {
                UserId = staffUserId,
                Type = "EquipmentIssue",
                Title = "Phản ánh sự cố thiết bị mới",
                Content = $"Thiết bị '{eq.Name}' vừa được báo hỏng: {issue.Description}",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        issue.Equipment = eq;
        return MapReport(issue);
    }

    public async Task<EquipmentReportDto?> UpdateReportStatusAsync(
        Guid reportId,
        Guid userId,
        string userRole,
        UpdateReportStatusDto dto,
        CancellationToken ct = default)
    {
        var issue = await _equipmentIssueRepository.GetByIdWithDetailsAsync(reportId, ct);
        if (issue == null) return null;

        var staff = await _staffRepository.GetByUserIdWithUserAsync(userId, ct);

        // Lễ tân chỉ được xử lý sự cố thuộc đúng chi nhánh mình phụ trách
        if (userRole.Equals("Staff", StringComparison.OrdinalIgnoreCase))
        {
            if (staff == null || staff.BranchId != issue.Equipment.BranchId)
                throw new UnauthorizedAccessException("Bạn chỉ có thể xử lý sự cố thiết bị tại chi nhánh của mình.");
        }

        var nextStatus = ParseIssueStatus(dto.Status);
        issue.Status = nextStatus;

        if (staff != null)
        {
            issue.HandledBy = staff.Id;
            var log = new EquipmentIssueLog
            {
                IssueId = issue.Id,
                StaffId = staff.Id,
                Note = dto.Note?.Trim() ?? string.Empty,
                StatusAfter = nextStatus,
                CreatedAt = DateTime.UtcNow
            };
            await _equipmentIssueRepository.AddLogAsync(log, ct);
            if (!issue.Logs.Contains(log))
                issue.Logs.Add(log);
        }

        // Đồng bộ trạng thái thiết bị theo tiến độ sửa chữa
        issue.Equipment.Status = nextStatus switch
        {
            IssueStatus.Investigating => EquipmentStatus.UnderMaintenance,
            IssueStatus.Fixed => EquipmentStatus.Operational,
            IssueStatus.Closed => EquipmentStatus.Operational,
            _ => EquipmentStatus.Broken
        };

        await _unitOfWork.SaveChangesAsync(ct);
        return MapReport(issue);
    }

    // --- Helper Mapping DB <-> Frontend ---
    private static EquipmentDto MapEquipment(Equipment e) => new(
        e.Id,
        e.BranchId,
        e.Name,
        e.Type,
        $"EQ-{e.Id.ToString()[..6].ToUpper()}",
        ToFrontendEquipmentStatus(e.Status),
        DateTime.UtcNow
    );

    private static EquipmentReportDto MapReport(EquipmentIssue i) => new(
        i.Id,
        i.EquipmentId,
        i.Equipment?.BranchId ?? Guid.Empty,
        i.ReportedBy,
        i.Description,
        ToFrontendReportStatus(i.Status),
        i.CreatedAt,
        i.Logs
            .OrderBy(l => l.CreatedAt)
            .Select(l => new EquipmentReportUpdateDto(
                l.CreatedAt,
                l.StaffId,
                l.Note,
                ToFrontendReportStatus(l.StatusAfter)
            ))
            .ToList()
    );

    private static string ToFrontendEquipmentStatus(EquipmentStatus s) => s switch
    {
        EquipmentStatus.Operational => "active",
        EquipmentStatus.UnderMaintenance => "repairing",
        EquipmentStatus.Broken => "broken",
        EquipmentStatus.Decommissioned => "retired",
        _ => "active"
    };

    private static EquipmentStatus ParseEquipmentStatus(string s) => s.Trim().ToLower() switch
    {
        "active" or "operational" => EquipmentStatus.Operational,
        "repairing" or "undermaintenance" => EquipmentStatus.UnderMaintenance,
        "broken" => EquipmentStatus.Broken,
        "retired" or "decommissioned" => EquipmentStatus.Decommissioned,
        _ => throw new ValidationException($"Trạng thái thiết bị không hợp lệ: {s}")
    };

    private static string ToFrontendReportStatus(IssueStatus s) => s switch
    {
        IssueStatus.Open => "pending",
        IssueStatus.Investigating => "in_progress",
        IssueStatus.Fixed => "resolved",
        IssueStatus.Closed => "rejected",
        _ => "pending"
    };

    private static IssueStatus ParseIssueStatus(string s) => s.Trim().ToLower() switch
    {
        "pending" or "open" => IssueStatus.Open,
        "in_progress" or "investigating" => IssueStatus.Investigating,
        "resolved" or "fixed" => IssueStatus.Fixed,
        "rejected" or "closed" => IssueStatus.Closed,
        _ => throw new ValidationException($"Trạng thái sự cố không hợp lệ: {s}")
    };
}

using OOAD.Domain.Entities;
using OOAD.Domain.Enums;

namespace OOAD.Domain.Interfaces;

public interface ICheckinRepository : IGenericRepository<Checkin>
{
    /// <summary>Lấy lượt check-in đang mở (chưa check-out) gần nhất của hội viên.</summary>
    Task<Checkin?> GetOpenCheckinByMemberIdAsync (
        Guid memberId,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy chi tiết 1 lượt check-in kèm Member, User, Branch, Card, Recorder.</summary>
    Task <Checkin?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách hội viên đang có mặt tại chi nhánh (CheckOutAt == null).</summary>
    Task<IReadOnlyList<Checkin>> GetActiveCheckinsAsync(
        Guid? branchId = null, 
        CancellationToken cancellationToken = default);

      /// <summary>Tra cứu lịch sử check-in có lọc theo chi nhánh, hình thức, ngày, từ khóa, hoặc hội viên.</summary>
    Task<IReadOnlyList<Checkin>> GetHistoryAsync(
        Guid? branchId = null,
        Guid? memberId = null,
        CheckinMethod? method = null,
        DateTime? fromDateUtc = null,
        DateTime? toDateUtc = null,
        string? search = null,
        int take = 200,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy tất cả lượt check-in chưa đóng để phục vụ Auto Check-out.</summary>
    Task<IReadOnlyList<Checkin>> GetAllOpenCheckinsWithBranchAsync(CancellationToken cancellationToken = default);
}

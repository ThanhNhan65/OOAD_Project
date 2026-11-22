using OOAD.Application.DTOs;

namespace OOAD.Application.Services.Interfaces;

public interface IMembershipSubscriptionService
{
    // Danh mục gói tập (MembershipPlans)
    Task<IReadOnlyList<MembershipPlanDto>> GetAllPlansAsync(string? planType = null, CancellationToken cancellationToken = default);
    Task<MembershipPlanDto?> GetPlanByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MembershipPlanDto> CreatePlanAsync(CreateUpdateMembershipPlanRequest request, CancellationToken cancellationToken = default);
    Task<MembershipPlanDto> UpdatePlanAsync(Guid id, CreateUpdateMembershipPlanRequest request, CancellationToken cancellationToken = default);
    Task DeletePlanAsync(Guid id, CancellationToken cancellationToken = default);

    // Đăng ký / Gia hạn gói tập & Thanh toán (Memberships & Payments)
    Task<IReadOnlyList<MembershipDto>> GetMyMembershipsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(MembershipDto Membership, PaymentDto Payment)> PurchaseAsync(Guid userId, PurchaseMembershipRequest request, CancellationToken cancellationToken = default);
    Task<(MembershipDto Membership, PaymentDto Payment)> RetryPaymentAsync(Guid userId, Guid membershipId, RetryPaymentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentDto>> GetMyPaymentsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentDto>> GetAllPaymentsAsync(Guid? branchId = null, CancellationToken cancellationToken = default);

    // Yêu cầu tạm ngưng gói (FreezeRequests)
    Task<IReadOnlyList<FreezeRequestDto>> GetMyFreezeRequestsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FreezeRequestDto>> GetAllFreezeRequestsAsync(string? status = null, CancellationToken cancellationToken = default);
    Task<FreezeRequestDto> CreateFreezeRequestAsync(Guid userId, CreateFreezeRequest request, CancellationToken cancellationToken = default);
    Task<FreezeRequestDto> HandleFreezeRequestAsync(Guid staffUserId, Guid freezeRequestId, HandleFreezeRequest request, CancellationToken cancellationToken = default);
}

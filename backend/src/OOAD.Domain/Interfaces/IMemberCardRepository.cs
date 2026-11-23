using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IMemberCardRepository
{
    Task<MemberCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemberCard>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(MemberCard entity, CancellationToken cancellationToken = default);
    void Update(MemberCard entity);
    void Delete(MemberCard entity);

    Task<MemberCard?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MemberCard?> GetByCardCodeAsync(string cardCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemberCard>> GetByMemberIdAsync(Guid memberId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemberCard>> GetCardsWithMemberAsync(
        Guid? memberId = null,
        string? search = null,
        string? status = null,
        CancellationToken cancellationToken = default);
    Task<bool> CardCodeExistsAsync(string cardCode, CancellationToken cancellationToken = default);
    Task<string> GenerateUniqueRandomCardCodeAsync(CancellationToken cancellationToken = default);
}

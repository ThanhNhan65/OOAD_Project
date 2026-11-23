using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IMemberRepository
{
    Task<Member?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Member entity, CancellationToken cancellationToken = default);
    void Update(Member entity);
    void Delete(Member entity);

    Task<Member?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Member?> GetByUserIdWithUserAsync(Guid userId, bool asNoTracking = false, CancellationToken cancellationToken = default);
    Task<Member?> GetByIdOrUserIdWithDetailsAsync(Guid idOrUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Member>> SearchMembersAsync(string? search = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetAllIdsAsync(CancellationToken cancellationToken = default);
    Task<string> GenerateNextCardCodeAsync(CancellationToken cancellationToken = default);
}

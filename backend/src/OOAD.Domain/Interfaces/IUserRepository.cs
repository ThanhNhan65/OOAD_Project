using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(User entity, CancellationToken cancellationToken = default);
    void Update(User entity);
    void Delete(User entity);

    Task<User?> GetByEmailOrUserNameAsync(string emailOrUserName, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetAllWithDetailsAsync(string? role = null, string? search = null, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> UserNameExistsAsync(string userName, CancellationToken cancellationToken = default);
}

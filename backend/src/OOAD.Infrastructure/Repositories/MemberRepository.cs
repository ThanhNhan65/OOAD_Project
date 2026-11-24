using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class MemberRepository : IMemberRepository
{
    private readonly AppDbContext _context;

    public MemberRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Member?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Members.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Members.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Member entity, CancellationToken cancellationToken = default)
    {
        await _context.Members.AddAsync(entity, cancellationToken);
    }

    public void Update(Member entity)
    {
        _context.Members.Update(entity);
    }

    public void Delete(Member entity)
    {
        _context.Members.Remove(entity);
    }

    public async Task<Member?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Members
            .Include(m => m.User)
            .Include(m => m.MemberCards)
            .Include(m => m.Memberships)
                .ThenInclude(ms => ms.Plan)
            .FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken);
    }

    public async Task<Member?> GetByUserIdWithUserAsync(
        Guid userId,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Members.Include(m => m.User).AsQueryable();
        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken);
    }

    public async Task<Member?> GetByIdOrUserIdWithDetailsAsync(Guid idOrUserId, CancellationToken cancellationToken = default)
    {
        return await _context.Members
            .Include(m => m.User)
            .Include(m => m.MemberCards)
            .Include(m => m.Memberships)
                .ThenInclude(ms => ms.Plan)
            .FirstOrDefaultAsync(m => m.Id == idOrUserId || m.UserId == idOrUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<Member>> SearchMembersAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Members
            .AsNoTracking()
            .Include(m => m.User)
            .Include(m => m.MemberCards)
            .Include(m => m.Memberships)
                .ThenInclude(ms => ms.Plan)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(m =>
                m.User.FullName.ToLower().Contains(term) ||
                m.User.Email.ToLower().Contains(term) ||
                (m.User.Phone != null && m.User.Phone.Contains(term)) ||
                m.MemberCards.Any(mc => mc.CardCode.ToLower().Contains(term)));
        }

        return await query
            .OrderByDescending(m => m.User.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetAllIdsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Members
            .AsNoTracking()
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<string> GenerateNextCardCodeAsync(CancellationToken cancellationToken = default)
    {
        string candidate;
        do
        {
            candidate = MemberCardRepository.GenerateCryptographicCardCode();
        } while (await _context.MemberCards.AnyAsync(mc => mc.CardCode == candidate, cancellationToken));

        return candidate;
    }
}

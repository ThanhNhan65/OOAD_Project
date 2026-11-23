using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly AppDbContext _context;

    public PaymentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Payments.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Payments.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Payment entity, CancellationToken cancellationToken = default)
    {
        await _context.Payments.AddAsync(entity, cancellationToken);
    }

    public void Update(Payment entity)
    {
        _context.Payments.Update(entity);
    }

    public void Delete(Payment entity)
    {
        _context.Payments.Remove(entity);
    }

    public async Task<IReadOnlyList<Payment>> GetByMemberIdWithDetailsAsync(
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .AsNoTracking()
            .Include(p => p.Membership).ThenInclude(m => m.Plan)
            .Include(p => p.Membership).ThenInclude(m => m.Branch)
            .Where(p => p.Membership.MemberId == memberId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetAllWithDetailsAsync(
        Guid? branchId = null,
        int take = 300,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Payments
            .AsNoTracking()
            .Include(p => p.Membership).ThenInclude(m => m.Plan)
            .Include(p => p.Membership).ThenInclude(m => m.Branch)
            .Include(p => p.Membership).ThenInclude(m => m.Member).ThenInclude(mb => mb.User)
            .AsQueryable();

        if (branchId.HasValue && branchId.Value != Guid.Empty)
            query = query.Where(p => p.Membership.BranchId == branchId.Value);

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(Math.Clamp(take, 1, 1000))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetCompletedWithDetailsAsync(
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Payments
            .AsNoTracking()
            .Include(p => p.Membership).ThenInclude(m => m.Plan)
            .Include(p => p.Membership).ThenInclude(m => m.Member).ThenInclude(mb => mb.User)
            .Where(p => p.Status == PaymentStatus.Completed);

        if (branchId.HasValue)
            query = query.Where(p => p.Membership.BranchId == branchId.Value);

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

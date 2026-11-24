using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class EquipmentRepository : IEquipmentRepository
{
    private readonly AppDbContext _context;

    public EquipmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Equipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Equipments.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Equipment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Equipments.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Equipment entity, CancellationToken cancellationToken = default)
    {
        await _context.Equipments.AddAsync(entity, cancellationToken);
    }

    public void Update(Equipment entity)
    {
        _context.Equipments.Update(entity);
    }

    public void Delete(Equipment entity)
    {
        _context.Equipments.Remove(entity);
    }

    public async Task<IReadOnlyList<Equipment>> GetAllByBranchAsync(
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Equipments.AsNoTracking();
        if (branchId.HasValue)
            query = query.Where(e => e.BranchId == branchId.Value);

        return await query.ToListAsync(cancellationToken);
    }
}

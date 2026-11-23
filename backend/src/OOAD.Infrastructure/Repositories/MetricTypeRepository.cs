using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class MetricTypeRepository : IMetricTypeRepository
{
    private readonly AppDbContext _context;

    public MetricTypeRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<MetricType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.MetricTypes.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<MetricType>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.MetricTypes.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(MetricType entity, CancellationToken cancellationToken = default)
    {
        await _context.MetricTypes.AddAsync(entity, cancellationToken);
    }

    public void Update(MetricType entity)
    {
        _context.MetricTypes.Update(entity);
    }

    public void Delete(MetricType entity)
    {
        _context.MetricTypes.Remove(entity);
    }

    public async Task<IReadOnlyList<MetricType>> GetActiveOrderedAsync(CancellationToken cancellationToken = default)
    {
        return await _context.MetricTypes
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }
}

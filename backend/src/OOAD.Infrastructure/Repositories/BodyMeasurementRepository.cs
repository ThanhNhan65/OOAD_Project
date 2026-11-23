using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class BodyMeasurementRepository : IBodyMeasurementRepository
{
    private readonly AppDbContext _context;

    public BodyMeasurementRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<BodyMeasurement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.BodyMeasurements.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<BodyMeasurement>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.BodyMeasurements.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BodyMeasurement entity, CancellationToken cancellationToken = default)
    {
        await _context.BodyMeasurements.AddAsync(entity, cancellationToken);
    }

    public void Update(BodyMeasurement entity)
    {
        _context.BodyMeasurements.Update(entity);
    }

    public void Delete(BodyMeasurement entity)
    {
        _context.BodyMeasurements.Remove(entity);
    }

    public async Task<IReadOnlyList<BodyMeasurement>> GetByMemberIdWithValuesAsync(
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        return await _context.BodyMeasurements
            .AsNoTracking()
            .Include(m => m.Values)
                .ThenInclude(v => v.MetricType)
            .Where(m => m.MemberId == memberId)
            .OrderBy(m => m.MeasuredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<BodyMeasurement?> GetByIdWithValuesAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.BodyMeasurements
            .AsNoTracking()
            .Include(m => m.Values)
                .ThenInclude(v => v.MetricType)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<BodyMeasurement?> GetByIdAndMemberIdAsync(
        Guid measurementId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        return await _context.BodyMeasurements
            .FirstOrDefaultAsync(m => m.Id == measurementId && m.MemberId == memberId, cancellationToken);
    }

    public async Task<bool> HasAnyForMemberAsync(
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        return await _context.BodyMeasurements.AnyAsync(m => m.MemberId == memberId, cancellationToken);
    }
}

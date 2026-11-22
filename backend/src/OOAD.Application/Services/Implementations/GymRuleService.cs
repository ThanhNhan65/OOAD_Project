using Microsoft.EntityFrameworkCore;
using OOAD.Application.Common.Interfaces;
using OOAD.Application.DTOs.GymRule;
using OOAD.Application.Services.Interfaces;
using OOAD.Domain.Entities;

namespace OOAD.Application.Services.Implementations;

public class GymRuleService : IGymRuleService
{
    private readonly IAppDbContext _context;

    public GymRuleService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<GymRuleDto>> GetAllAsync(
        Guid? branchId = null,
        string? category = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.GymRules
            .AsNoTracking()
            .Include(r => r.Branch)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        if (branchId.HasValue)
        {
            query = query.Where(r => r.BranchId == null || r.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToUpper();
            query = query.Where(r => r.Category.ToUpper() == cat);
        }

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new GymRuleDto(
                r.Id,
                r.BranchId,
                r.Branch != null ? r.Branch.Name : null,
                r.CreatedBy,
                r.Title,
                r.Content,
                r.Category,
                r.IsActive,
                r.CreatedAt,
                r.UpdatedAt
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<GymRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var r = await _context.GymRules
            .AsNoTracking()
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (r == null) return null;

        return new GymRuleDto(
            r.Id,
            r.BranchId,
            r.Branch?.Name,
            r.CreatedBy,
            r.Title,
            r.Content,
            r.Category,
            r.IsActive,
            r.CreatedAt,
            r.UpdatedAt
        );
    }

    public async Task<GymRuleDto> CreateAsync(CreateGymRuleDto dto, Guid? createdBy, CancellationToken cancellationToken = default)
    {
        var rule = new GymRule
        {
            BranchId = dto.BranchId,
            CreatedBy = createdBy,
            Title = dto.Title.Trim(),
            Content = dto.Content.Trim(),
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "GENERAL" : dto.Category.Trim().ToUpper(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.GymRules.Add(rule);
        await _context.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(rule.Id, cancellationToken))!;
    }

    public async Task<GymRuleDto?> UpdateAsync(Guid id, UpdateGymRuleDto dto, CancellationToken cancellationToken = default)
    {
        var rule = await _context.GymRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rule == null) return null;

        rule.BranchId = dto.BranchId;
        rule.Title = dto.Title.Trim();
        rule.Content = dto.Content.Trim();
        rule.Category = string.IsNullOrWhiteSpace(dto.Category) ? "GENERAL" : dto.Category.Trim().ToUpper();
        rule.IsActive = dto.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(rule.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await _context.GymRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rule == null) return false;

        _context.GymRules.Remove(rule);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
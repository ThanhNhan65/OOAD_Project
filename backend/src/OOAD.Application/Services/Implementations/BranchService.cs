using OOAD.Application.DTOs.Branch;
using OOAD.Application.Services.Interfaces;
using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;

namespace OOAD.Application.Services.Implementations;

public class BranchService : IBranchService
{
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BranchService(IBranchRepository branchRepository, IUnitOfWork unitOfWork)
    {
        _branchRepository = branchRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<BranchDto>> GetAllBranchesAsync(CancellationToken cancellationToken = default)
    {
        var branches = await _branchRepository.GetAllAsync(cancellationToken);
        return branches.Select(MapToDto);
    }

    public async Task<BranchDto?> GetBranchByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var branch = await _branchRepository.GetByIdAsync(id, cancellationToken);
        return branch == null ? null : MapToDto(branch);
    }

    public async Task<BranchDto> CreateBranchAsync(CreateBranchDto dto, CancellationToken cancellationToken = default)
    {
        var branch = new Branch
        {
            Name = dto.Name,
            District = dto.District,
            Address = dto.Address,
            PhoneNumber = dto.PhoneNumber,
            OperatingHours = dto.OperatingHours,
            IsActive = true
        };

        await _branchRepository.AddAsync(branch, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(branch);
    }

    public async Task<BranchDto?> UpdateBranchAsync(Guid id, UpdateBranchDto dto, CancellationToken cancellationToken = default)
    {
        var branch = await _branchRepository.GetByIdAsync(id, cancellationToken);
        if (branch == null) return null;

        branch.Name = dto.Name;
        branch.District = dto.District;
        branch.Address = dto.Address;
        branch.PhoneNumber = dto.PhoneNumber;
        branch.OperatingHours = dto.OperatingHours;
        branch.IsActive = dto.IsActive;

        _branchRepository.Update(branch);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(branch);
    }

    private static BranchDto MapToDto(Branch branch) => new(
        branch.Id,
        branch.Name,
        branch.District,
        branch.Address,
        branch.PhoneNumber,
        branch.OperatingHours,
        branch.IsActive
    );
}

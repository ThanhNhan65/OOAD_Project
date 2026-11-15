using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class AssessmentNoteRepository : GenericRepository<AssessmentNote>, IAssessmentNoteRepository
{
    public AssessmentNoteRepository(AppDbContext context) : base(context)
    {
    }
}

using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Application.DTOs.Problem;
using AlgoJudge.Application.Interfaces;
using AutoMapper;

namespace AlgoJudge.Application.Services
{
    public class ProblemService : IProblemService
    {
        private readonly IProblemRepository _repository;
        private readonly IMapper _mapper;

        public ProblemService(IProblemRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<PagedResult<ProblemDto>> GetProblemAsync(int pageNumber, int pageSize)
        {
            var pagedEntities = await _repository.GetPagedAsync(pageNumber, pageSize);

            return new PagedResult<ProblemDto>
            {
                Items = _mapper.Map<IEnumerable<ProblemDto>>(pagedEntities.Items).ToList(),
                TotalCount = pagedEntities.TotalCount,
                PageNumber = pagedEntities.PageNumber,
                PageSize = pagedEntities.PageSize
            };
        }

        public async Task<ProblemDto?> GetProblemByIdAsync(int id)
        {
            var problem = await _repository.GetByIdAsync(id);
            return problem == null ? null : _mapper.Map<ProblemDto>(problem);
        }
    }
}

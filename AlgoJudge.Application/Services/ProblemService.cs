using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Application.DTOs.Problem;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Services
{
    public class ProblemService : IProblemService
    {
        private readonly IProblemRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ProblemService(IProblemRepository repository, IUnitOfWork unitOfWork, IMapper mapper)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<ProblemDto> CreateProblemAsync(CreateProblemDto dto)
        {
            var problem = _mapper.Map<Problem>(dto);

            await _repository.CreateAsync(problem);

            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<ProblemDto>(problem);
        }

        public async Task<PagedResult<ProblemDto>> GetProblemAsync(int pageNumber, int pageSize)
        {
            var pagedEntities = await _repository.GetPagedAsync(pageNumber, pageSize);

            var dtoItems = _mapper.Map<IEnumerable<ProblemDto>>(pagedEntities.Items).ToList();

            return new PagedResult<ProblemDto>
            {
                Items = dtoItems,
                TotalCount = pagedEntities.TotalCount,
                PageNumber = pagedEntities.PageNumber,
                PageSize = pagedEntities.PageSize
            };
        }

        public async Task<ProblemDto?> GetProblemByIdAsync(int id)
        {
            var problem = await _repository.GetByIdAsync(id);

            if (problem == null) return null;

            return _mapper.Map<ProblemDto>(problem);
        }
    }
}
